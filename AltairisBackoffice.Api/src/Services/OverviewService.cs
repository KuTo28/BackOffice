using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Overview;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Queries dashboard operational counts, booking trends, and recent booking details.</summary>
public sealed class OverviewService(AltairisDbContext dbContext) : IOverviewService
{
    private readonly AltairisDbContext _dbContext = dbContext;

    /// <summary>Builds the global overview, keeping operational counts separate from the trend interval.</summary>
    public async Task<OverviewResponse> GetOverviewAsync(
        DateOnly date,
        DateOnly? trendDate,
        CancellationToken cancellationToken)
    {
        return await BuildOverviewAsync(date, trendDate ?? date, null, cancellationToken);
    }

    /// <summary>Builds an overview scoped to an existing hotel.</summary>
    public async Task<OverviewResponse?> GetHotelOverviewAsync(
        long hotelId,
        DateOnly date,
        DateOnly? trendDate,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Hotels.AnyAsync(hotel => hotel.Id == hotelId, cancellationToken))
        {
            return null;
        }

        return await BuildOverviewAsync(date, trendDate ?? date, hotelId, cancellationToken);
    }

    private async Task<OverviewResponse> BuildOverviewAsync(
        DateOnly date,
        DateOnly trendDate,
        long? hotelId,
        CancellationToken cancellationToken)
    {
        var summary = await GetSummaryAsync(date, hotelId, cancellationToken);
        var bookingsQuery = _dbContext.Bookings.AsNoTracking();
        if (hotelId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(booking => booking.HotelId == hotelId.Value);
        }

        // The trend ends on trendDate and always contains seven inclusive calendar days.
        var trendStart = trendDate.AddDays(-6).ToDateTime(TimeOnly.MinValue);
        var trendEnd = trendDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var bookingsPerDay = await bookingsQuery
            .Where(booking =>
                booking.BookingDate.HasValue &&
                booking.BookingDate.Value >= trendStart &&
                booking.BookingDate.Value < trendEnd)
            .GroupBy(booking => booking.BookingDate!.Value.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var bookingDayLookup = bookingsPerDay.ToDictionary(
            item => DateOnly.FromDateTime(item.Date),
            item => item.Count);
        var bookingTrend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = trendDate.AddDays(offset - 6);
                return new OverviewBookingDayResponse(day, bookingDayLookup.GetValueOrDefault(day));
            })
            .ToList();

        var recentBookings = await bookingsQuery
            .OrderByDescending(booking => booking.BookingDate)
            .ThenByDescending(booking => booking.Id)
            .Take(6)
            .Select(booking => new
            {
                booking.Id,
                booking.HotelId,
                HotelName = booking.Hotel.Name,
                booking.BookingDate,
                booking.CheckIn,
                booking.CheckOut,
                booking.Status,
                booking.TotalPrice,
                booking.GuestName,
                booking.GuestPhone,
                booking.GuestEmail
            })
            .ToListAsync(cancellationToken);

        var bookingIds = recentBookings.Select(booking => booking.Id).ToList();
        var roomsByBooking = await _dbContext.BookedRooms
            .AsNoTracking()
            .Where(room => bookingIds.Contains(room.BookingId))
            .Select(room => new { room.BookingId, room.RoomId, room.Name, room.Price })
            .ToListAsync(cancellationToken);

        var roomLookup = roomsByBooking
            .GroupBy(room => room.BookingId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<OverviewRoomResponse>)group
                    .Select(room => new OverviewRoomResponse(room.RoomId, room.Name, room.Price))
                    .ToList());
        var bookingResponses = recentBookings
            .Select(booking => new OverviewBookingResponse(
                booking.Id,
                booking.HotelId,
                booking.HotelName,
                booking.BookingDate,
                booking.CheckIn,
                booking.CheckOut,
                booking.Status,
                booking.TotalPrice,
                booking.GuestName,
                booking.GuestPhone,
                booking.GuestEmail,
                roomLookup.GetValueOrDefault(booking.Id) ?? []))
            .ToList();

        return new OverviewResponse(
            summary.Arrivals,
            summary.Departures,
            summary.Booked,
            summary.Canceled,
            bookingTrend,
            bookingResponses);
    }

    /// <summary>Gets overall booking counts for the requested business date.</summary>
    public async Task<OverviewSummaryResponse> GetSummaryAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        return await GetSummaryAsync(date, null, cancellationToken);
    }

    private async Task<OverviewSummaryResponse> GetSummaryAsync(
        DateOnly date,
        long? hotelId,
        CancellationToken cancellationToken)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var bookingsQuery = _dbContext.Bookings.AsNoTracking();
        if (hotelId.HasValue)
        {
            bookingsQuery = bookingsQuery.Where(booking => booking.HotelId == hotelId.Value);
        }

        var arrivals = await bookingsQuery.CountAsync(
            booking => booking.Status == "booked" && booking.CheckIn >= start && booking.CheckIn < end,
            cancellationToken);
        var departures = await bookingsQuery.CountAsync(
            booking => booking.Status == "booked" && booking.CheckOut >= start && booking.CheckOut < end,
            cancellationToken);
        var booked = await bookingsQuery.CountAsync(booking => booking.Status == "booked", cancellationToken);
        var canceled = await bookingsQuery.CountAsync(booking => booking.Status == "canceled", cancellationToken);

        return new OverviewSummaryResponse(arrivals, departures, booked, canceled);
    }
}
