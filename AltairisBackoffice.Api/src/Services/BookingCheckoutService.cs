using System.Data;
using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Coordinates cached availability, in-memory quotes, and transactional booking confirmation.</summary>
public sealed class BookingCheckoutService(
    AltairisDbContext dbContext,
    IHotelInventoryCache inventoryCache,
    IBookingQuoteStore quoteStore,
    ILogger<BookingCheckoutService> logger) : IBookingCheckoutService
{
    private static readonly TimeSpan QuoteLifetime = TimeSpan.FromSeconds(30);
    private readonly AltairisDbContext _dbContext = dbContext;

    /// <summary>Returns a fast availability snapshot; a quote must still be validated before booking.</summary>
    public Task<(BookingCheckoutStatus Status, BookingAvailabilityResponse? Response)> GetAvailabilityAsync(
        long hotelId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidStay(hotelId, checkIn, checkOut))
        {
            return Task.FromResult<(BookingCheckoutStatus, BookingAvailabilityResponse?)>(
                (BookingCheckoutStatus.InvalidStay, null));
        }

        if (!inventoryCache.TryGet(hotelId, out var hotel))
        {
            return Task.FromResult<(BookingCheckoutStatus, BookingAvailabilityResponse?)>(
                (BookingCheckoutStatus.HotelNotFound, null));
        }

        if (!hotel!.IsActive)
        {
            return Task.FromResult<(BookingCheckoutStatus, BookingAvailabilityResponse?)>(
                (BookingCheckoutStatus.HotelInactive, null));
        }

        var nights = checkOut.DayNumber - checkIn.DayNumber;
        var rooms = hotel.Rooms
            .Select(room =>
            {
                var roomNights = EnumerateNights(checkIn, checkOut)
                    .Select(date => room.Nights.TryGetValue(date, out var night) ? night : null)
                    .ToList();
                var configured = roomNights.All(night => night is not null);
                var available = configured && roomNights.All(night =>
                    night!.FreeSale || night.Capacity - night.Used > 0);
                var limitedCapacity = roomNights
                    .Where(night => night is { FreeSale: false })
                    .Select(night => night!.Capacity - night.Used)
                    .ToList();

                return new AvailableRoomResponse(
                    room.Id,
                    room.Name,
                    room.Description,
                    room.Price,
                    available,
                    configured ? limitedCapacity.Count == 0 ? null : Math.Max(0, limitedCapacity.Min()) : 0,
                    roomNights.Any(night => night?.FreeSale == true));
            })
            .ToList();

        return Task.FromResult<(BookingCheckoutStatus, BookingAvailabilityResponse?)>(
            (BookingCheckoutStatus.Success,
                new BookingAvailabilityResponse(hotelId, checkIn, checkOut, nights, rooms)));
    }

    /// <summary>Validates availability against persisted inventory and creates a 30-second in-memory quote.</summary>
    public async Task<(BookingCheckoutStatus Status, BookingQuoteResponse? Response)> CreateQuoteAsync(
        CreateBookingQuoteRequest request,
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        if (!IsValidStay(request.HotelId, request.CheckIn, request.CheckOut) ||
            request.RoomIds.Count == 0 ||
            request.RoomIds.Any(id => id <= 0) ||
            request.RoomIds.Distinct().Count() != request.RoomIds.Count)
        {
            return (BookingCheckoutStatus.InvalidStay, null);
        }

        var validation = await ValidateAgainstDatabaseAsync(
            request.HotelId, request.CheckIn, request.CheckOut, request.RoomIds, cancellationToken);
        if (validation.Status != BookingCheckoutStatus.Success)
        {
            return (validation.Status, null);
        }

        var validatedRooms = validation.Rooms
            ?? throw new InvalidOperationException("A successful stay validation must include its rooms.");
        var now = DateTimeOffset.UtcNow;
        var nights = request.CheckOut.DayNumber - request.CheckIn.DayNumber;
        var quote = new BookingQuoteResponse(
            quoteId,
            request.HotelId,
            request.CheckIn,
            request.CheckOut,
            nights,
            validatedRooms.Sum(room => room.Price * nights),
            now.Add(QuoteLifetime),
            validatedRooms
                .OrderBy(room => room.Name)
                .Select(room => new BookingQuoteRoomResponse(
                    room.Id,
                    room.Name,
                    room.Description,
                    room.Price,
                    room.Price * nights))
                .ToList());

        quoteStore.Add(quote);
        return (BookingCheckoutStatus.Success, quote);
    }

    /// <summary>Returns a quote until expiry, retaining its details after expiry if it was confirmed.</summary>
    public async Task<(BookingCheckoutStatus Status, BookingQuoteResponse? Response)> GetQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!quoteStore.TryGet(quoteId, out var quoteState))
        {
            return (BookingCheckoutStatus.QuoteNotFound, null);
        }

        var quote = quoteState!.Quote;
        if (quote.ExpiresAt <= DateTimeOffset.UtcNow && quoteState.ConfirmedBooking is null)
        {
            return (BookingCheckoutStatus.QuoteExpired, null);
        }

        return (BookingCheckoutStatus.Success, quote);
    }

    /// <summary>Serializes confirmation per quote and rechecks inventory inside a serializable transaction.</summary>
    public async Task<(BookingCheckoutStatus Status, ConfirmedBookingResponse? Response)> ConfirmAsync(
        Guid quoteId,
        ConfirmBookingQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!quoteStore.TryGet(quoteId, out var quoteState))
        {
            return (BookingCheckoutStatus.QuoteNotFound, null);
        }

        await quoteState!.ConfirmationLock.WaitAsync(cancellationToken);
        try
        {
            var quote = quoteState.Quote;
            if (quoteState.ConfirmedBooking is not null)
            {
                return (BookingCheckoutStatus.AlreadyConfirmed, quoteState.ConfirmedBooking with
                {
                    AlreadyConfirmed = true
                });
            }

            if (quote.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return (BookingCheckoutStatus.QuoteExpired, null);
            }

            // Serialize database checks and writes so concurrent confirmations cannot consume the same inventory.
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var validation = await ValidateAgainstDatabaseAsync(
                quote.HotelId,
                quote.CheckIn,
                quote.CheckOut,
                quote.Rooms.Select(room => room.RoomId).ToArray(),
                cancellationToken);
            if (validation.Status != BookingCheckoutStatus.Success)
            {
                return (validation.Status, null);
            }

            if (quote.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return (BookingCheckoutStatus.QuoteExpired, null);
            }

            var booking = new Booking
            {
                HotelId = quote.HotelId,
                BookingDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                CheckIn = quote.CheckIn.ToDateTime(TimeOnly.MinValue),
                CheckOut = quote.CheckOut.ToDateTime(TimeOnly.MinValue),
                GuestEmail = request.GuestEmail,
                GuestPhone = request.GuestPhone,
                GuestName = request.GuestName,
                Status = "booked",
                TotalPrice = quote.TotalPrice
            };

            _dbContext.Bookings.Add(booking);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var room in quote.Rooms)
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO booked_rooms (booking_id, room_id, price, name, description)
                    VALUES ({booking.Id}, {room.RoomId}, {room.PricePerNight}, {room.Name}, {room.Description})
                    """, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            var confirmedBooking = new ConfirmedBookingResponse(
                booking.Id, quote.QuoteId, booking.Status!, quote.TotalPrice, false);
            quoteState.ConfirmedBooking = confirmedBooking;
            await inventoryCache.RefreshHotelAsync(quote.HotelId, CancellationToken.None);

            return (BookingCheckoutStatus.Success, confirmedBooking);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            logger.LogInformation(exception, "Concurrent booking confirmation rejected for quote {QuoteId}.", quoteId);
            return (BookingCheckoutStatus.ConcurrentUpdate, null);
        }
        finally
        {
            quoteState.ConfirmationLock.Release();
        }
    }

    private async Task<StayValidationResult> ValidateAgainstDatabaseAsync(
        long hotelId,
        DateOnly checkIn,
        DateOnly checkOut,
        IReadOnlyCollection<long> roomIds,
        CancellationToken cancellationToken)
    {
        var hotel = await _dbContext.Hotels
            .AsNoTracking()
            .Where(item => item.Id == hotelId)
            .Select(item => new { item.Id, Active = item.Active ?? false })
            .FirstOrDefaultAsync(cancellationToken);
        if (hotel is null)
        {
            return new StayValidationResult(BookingCheckoutStatus.HotelNotFound);
        }

        if (!hotel.Active)
        {
            return new StayValidationResult(BookingCheckoutStatus.HotelInactive);
        }

        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.HotelId == hotelId && roomIds.Contains(room.Id))
            .Select(room => new QuotedRoom(room.Id, room.Name, room.Description, room.Price))
            .ToListAsync(cancellationToken);
        if (rooms.Count != roomIds.Count)
        {
            return new StayValidationResult(BookingCheckoutStatus.InvalidRoomSelection);
        }

        var lastNight = checkOut.AddDays(-1);
        // Every room must have a dated inventory row for every night; checkout day is intentionally excluded.
        var inventory = await _dbContext.RoomInventories
            .AsNoTracking()
            .Where(item => roomIds.Contains(item.RoomId) &&
                item.Date >= checkIn && item.Date <= lastNight)
            .Select(item => new InventoryNight(item.RoomId, item.Date, item.Capacity, item.FreeSale))
            .ToListAsync(cancellationToken);
        if (inventory.Count != roomIds.Count * (lastNight.DayNumber - checkIn.DayNumber + 1))
        {
            return new StayValidationResult(BookingCheckoutStatus.Unavailable);
        }

        var overlappingStays = await _dbContext.BookedRooms
            .AsNoTracking()
            .Where(room => room.RoomId != null &&
                roomIds.Contains(room.RoomId.Value) &&
                room.Booking.Status == "booked" &&
                room.Booking.CheckIn < checkOut.ToDateTime(TimeOnly.MinValue) &&
                room.Booking.CheckOut > checkIn.ToDateTime(TimeOnly.MinValue))
            .Select(room => new BookingStay(
                room.RoomId!.Value,
                room.Booking.CheckIn,
                room.Booking.CheckOut))
            .ToListAsync(cancellationToken);
        var bookedCounts = CountBookedNights(overlappingStays, checkIn, lastNight);

        foreach (var item in inventory.Where(item => !item.FreeSale))
        {
            var booked = bookedCounts.GetValueOrDefault((item.RoomId, item.Date));
            if (item.Capacity - booked < 1)
            {
                return new StayValidationResult(BookingCheckoutStatus.Unavailable);
            }
        }

        return new StayValidationResult(BookingCheckoutStatus.Success, rooms);
    }

    private static bool IsValidStay(long hotelId, DateOnly checkIn, DateOnly checkOut) =>
        hotelId > 0 &&
        checkIn < checkOut &&
        checkOut < DateOnly.MaxValue &&
        checkOut.DayNumber - checkIn.DayNumber <= 366;

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException
        {
            InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        };

    private static IEnumerable<DateOnly> EnumerateNights(DateOnly checkIn, DateOnly checkOut)
    {
        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static Dictionary<(long RoomId, DateOnly Date), int> CountBookedNights(
        IEnumerable<BookingStay> stays,
        DateOnly from,
        DateOnly to)
    {
        var counts = new Dictionary<(long RoomId, DateOnly Date), int>();
        foreach (var stay in stays)
        {
            if (stay.CheckIn is null || stay.CheckOut is null)
            {
                continue;
            }

            var first = DateOnly.FromDateTime(stay.CheckIn.Value);
            var last = DateOnly.FromDateTime(stay.CheckOut.Value).AddDays(-1);
            if (first < from) first = from;
            if (last > to) last = to;

            for (var date = first; date <= last; date = date.AddDays(1))
            {
                var key = (stay.RoomId, date);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        return counts;
    }

    private sealed record StayValidationResult(
        BookingCheckoutStatus Status,
        IReadOnlyList<QuotedRoom>? Rooms = null);

    private sealed record QuotedRoom(long Id, string Name, string? Description, double Price);

    private sealed record InventoryNight(long RoomId, DateOnly Date, int Capacity, bool FreeSale);

    private sealed record BookingStay(long RoomId, DateTime? CheckIn, DateTime? CheckOut);
}
