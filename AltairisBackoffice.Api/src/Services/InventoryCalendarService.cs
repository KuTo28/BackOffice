using System.Data;
using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Reads dated inventory and applies atomic, booking-safe inventory changes.</summary>
public sealed class InventoryCalendarService(
    AltairisDbContext dbContext,
    IHotelInventoryCache inventoryCache) : IInventoryCalendarService
{
    private readonly AltairisDbContext _dbContext = dbContext;
    private readonly IHotelInventoryCache _inventoryCache = inventoryCache;

    public async Task<IReadOnlyList<InventoryDayResponse>> GetInventoryAsync(
        long hotelId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadInventoryAsync(hotelId, null, from, to, cancellationToken);
        return snapshot.Items;
    }

    public async Task<IReadOnlyList<InventoryCalendarDayResponse>?> GetCalendarAsync(
        long hotelId,
        long? roomId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Hotels.AnyAsync(hotel => hotel.Id == hotelId, cancellationToken))
        {
            return null;
        }

        var snapshot = await LoadInventoryAsync(hotelId, roomId, from, to, cancellationToken);
        if (roomId.HasValue && snapshot.RoomIds.Count == 0)
        {
            return null;
        }
        var inventoryByDate = snapshot.Items
            .GroupBy(item => item.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        var days = new List<InventoryCalendarDayResponse>(to.DayNumber - from.DayNumber + 1);

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var records = inventoryByDate.GetValueOrDefault(date) ?? [];
            var configured = records.Count > 0;
            var incomplete = !roomId.HasValue && configured && records.Count < snapshot.RoomIds.Count;
            var includesFreeSale = records.Any(item => item.FreeSale);
            int? configuredCapacity = configured ? records.Sum(item => item.Capacity) : null;
            // A finite aggregate cannot be reported if any included room type uses free sale.
            int? capacity = configured && !includesFreeSale ? records.Sum(item => item.Capacity) : null;
            var bookingCount = records.Sum(item => item.Booked);
            int? available = capacity.HasValue ? Math.Max(capacity.Value - bookingCount, 0) : null;
            var used = capacity.HasValue ? capacity.Value - available!.Value : bookingCount;
            var unavailable = !configured || incomplete || (!includesFreeSale && available == 0);
            // Mark limited inventory when at most 20% (and at least one unit) remains.
            var limited = !unavailable &&
                !includesFreeSale &&
                available <= Math.Max(1, (int)Math.Ceiling(capacity!.Value * 0.2));
            var roomBreakdown = snapshot.Rooms
                .Select(room =>
                {
                    var roomRecord = records.FirstOrDefault(item => item.RoomId == room.Id);
                    var roomConfigured = roomRecord is not null;
                    var roomUsed = snapshot.BookedCounts.GetValueOrDefault((room.Id, date));
                    var roomFreeSale = roomRecord?.FreeSale ?? false;
                    var roomCapacity = roomRecord?.Capacity;
                    return new InventoryRoomCalendarResponse(
                        room.Id,
                        room.Name,
                        roomCapacity,
                        roomConfigured && !roomFreeSale && roomCapacity.HasValue
                            ? Math.Max(roomCapacity.Value - roomUsed, 0)
                            : null,
                        roomUsed,
                        roomConfigured,
                        roomFreeSale);
                })
                .ToList();

            days.Add(new InventoryCalendarDayResponse(
                date,
                capacity,
                configuredCapacity,
                available,
                used,
                configured,
                incomplete,
                includesFreeSale,
                unavailable ? "unavailable" : limited ? "limited" : "available",
                roomBreakdown));
        }

        return days;
    }

    public async Task<InventoryUpdateResult> UpdateInventoryAsync(
        long roomId,
        InventoryUpdateCommand command,
        CancellationToken cancellationToken) =>
        await UpdateInventoryAsync([roomId], command, cancellationToken);

    public async Task<InventoryUpdateResult> UpdateInventoryAsync(
        IReadOnlyCollection<long> roomIds,
        InventoryUpdateCommand command,
        CancellationToken cancellationToken)
    {
        var distinctRoomIds = roomIds.Distinct().ToArray();
        if (distinctRoomIds.Length == 0)
        {
            return new InventoryUpdateResult(InventoryUpdateStatus.RoomNotFound);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var existingRoomCount = await _dbContext.Rooms
                .AsNoTracking()
                .CountAsync(item => distinctRoomIds.Contains(item.Id), cancellationToken);
            if (existingRoomCount != distinctRoomIds.Length)
            {
                return new InventoryUpdateResult(InventoryUpdateStatus.RoomNotFound);
            }

            var hotelIds = await _dbContext.Rooms
                .AsNoTracking()
                .Where(item => distinctRoomIds.Contains(item.Id))
                .Select(item => item.HotelId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var endExclusive = command.To.AddDays(1);
            var existing = await _dbContext.RoomInventories
                .Where(item => distinctRoomIds.Contains(item.RoomId) &&
                    item.Date >= command.From && item.Date <= command.To)
                .ToDictionaryAsync(item => (item.RoomId, item.Date), cancellationToken);

            var bookedStays = await _dbContext.BookedRooms
                .AsNoTracking()
                .Where(bookedRoom => bookedRoom.RoomId != null &&
                    distinctRoomIds.Contains(bookedRoom.RoomId.Value) &&
                    bookedRoom.Booking.Status == "booked" &&
                    bookedRoom.Booking.CheckIn < endExclusive.ToDateTime(TimeOnly.MinValue) &&
                    bookedRoom.Booking.CheckOut > command.From.ToDateTime(TimeOnly.MinValue))
                .Select(bookedRoom => new BookedStay(
                    bookedRoom.RoomId!.Value,
                    bookedRoom.Booking.CheckIn,
                    bookedRoom.Booking.CheckOut))
                .ToListAsync(cancellationToken);
            var bookedCounts = CountBookedNights(bookedStays, command.From, command.To);

            foreach (var roomId in distinctRoomIds)
            {
                for (var date = command.From; date <= command.To; date = date.AddDays(1))
                {
                    var booked = bookedCounts.GetValueOrDefault((roomId, date));
                    if (!command.FreeSale && command.Capacity < booked)
                    {
                        return new InventoryUpdateResult(
                            InventoryUpdateStatus.CapacityConflict, date, booked);
                    }

                    if (!existing.TryGetValue((roomId, date), out var item))
                    {
                        item = new RoomInventory { RoomId = roomId, Date = date };
                        _dbContext.RoomInventories.Add(item);
                    }

                    item.Capacity = command.Capacity;
                    item.FreeSale = command.FreeSale;
                }
            }

            // Commit the full room/date batch before refreshing each affected hotel's availability snapshot.
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            foreach (var hotelId in hotelIds)
            {
                await _inventoryCache.RefreshHotelAsync(hotelId, CancellationToken.None);
            }
            return new InventoryUpdateResult(InventoryUpdateStatus.Updated);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new InventoryUpdateResult(InventoryUpdateStatus.ConcurrentUpdate);
        }
    }

    private async Task<InventorySnapshot> LoadInventoryAsync(
        long hotelId,
        long? roomId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var roomsQuery = _dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.HotelId == hotelId);
        if (roomId.HasValue)
        {
            roomsQuery = roomsQuery.Where(room => room.Id == roomId.Value);
        }

        var rooms = await roomsQuery
            .OrderBy(room => room.Name)
            .Select(room => new InventoryRoom(room.Id, room.Name))
            .ToListAsync(cancellationToken);
        var roomIds = rooms.Select(room => room.Id).ToList();
        var inventoryRows = await _dbContext.RoomInventories
            .AsNoTracking()
            .Where(item => roomIds.Contains(item.RoomId) && item.Date >= from && item.Date <= to)
            .OrderBy(item => item.Room.Name)
            .ThenBy(item => item.Date)
            .Select(item => new InventoryRow(
                item.RoomId, item.Room.Name, item.Date, item.Capacity, item.FreeSale))
            .ToListAsync(cancellationToken);

        var endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var bookedStays = await _dbContext.BookedRooms
            .AsNoTracking()
            .Where(room => room.RoomId != null &&
                roomIds.Contains(room.RoomId.Value) &&
                room.Booking.Status == "booked" &&
                room.Booking.CheckIn < endExclusive &&
                room.Booking.CheckOut > from.ToDateTime(TimeOnly.MinValue))
            .Select(room => new BookedStay(room.RoomId!.Value, room.Booking.CheckIn, room.Booking.CheckOut))
            .ToListAsync(cancellationToken);

        var bookedCounts = CountBookedNights(bookedStays, from, to);
        var items = inventoryRows
            .Select(item =>
            {
                var booked = bookedCounts.GetValueOrDefault((item.RoomId, item.Date));
                return new InventoryDayResponse(
                    item.RoomId,
                    item.RoomName,
                    item.Date,
                    item.Capacity,
                    booked,
                    item.FreeSale ? null : Math.Max(item.Capacity - booked, 0),
                    item.FreeSale);
            })
            .ToList();

        return new InventorySnapshot(roomIds, rooms, items, bookedCounts);
    }

    private static Dictionary<(long RoomId, DateOnly Date), int> CountBookedNights(
        IEnumerable<BookedStay> stays,
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
            var last = DateOnly.FromDateTime(stay.CheckOut.Value);
            for (var date = first < from ? from : first; date < last && date <= to; date = date.AddDays(1))
            {
                var key = (stay.RoomId, date);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        return counts;
    }

    private sealed record InventoryRow(long RoomId, string RoomName, DateOnly Date, int Capacity, bool FreeSale);
    private sealed record BookedStay(long RoomId, DateTime? CheckIn, DateTime? CheckOut);
    private sealed record InventoryRoom(long Id, string Name);
    private sealed record InventorySnapshot(
        IReadOnlyList<long> RoomIds,
        IReadOnlyList<InventoryRoom> Rooms,
        IReadOnlyList<InventoryDayResponse> Items,
        IReadOnlyDictionary<(long RoomId, DateOnly Date), int> BookedCounts);
}
