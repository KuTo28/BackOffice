using System.Collections.Concurrent;
using AltairisBackoffice.Api.Database.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Cached room capacity and usage for one night.</summary>
public sealed record CachedRoomNight(int Capacity, bool FreeSale, int Used);

/// <summary>Cached room type and its configured nightly inventory.</summary>
public sealed record CachedRoom(
    long Id,
    string Name,
    string? Description,
    double Price,
    IReadOnlyDictionary<DateOnly, CachedRoomNight> Nights);

/// <summary>Availability snapshot used to serve fast hotel availability lookups.</summary>
public sealed record CachedHotelInventory(
    long HotelId,
    bool IsActive,
    IReadOnlyList<CachedRoom> Rooms);

/// <summary>Reads and refreshes the in-memory inventory snapshots keyed by hotel identifier.</summary>
public interface IHotelInventoryCache
{
    /// <summary>Gets the latest cached snapshot for a hotel, when present.</summary>
    bool TryGet(long hotelId, out CachedHotelInventory? inventory);

    /// <summary>Reloads one hotel's rooms, inventory, and active booked usage from the database.</summary>
    Task RefreshHotelAsync(long hotelId, CancellationToken cancellationToken);
}

/// <summary>Maintains an inventory snapshot per hotel and refreshes snapshots after data changes.</summary>
public sealed class HotelInventoryCache(
    IServiceScopeFactory scopeFactory,
    ILogger<HotelInventoryCache> logger) : IHotelInventoryCache, IHostedService
{
    private readonly ConcurrentDictionary<long, CachedHotelInventory> _hotels = new();
    private readonly ConcurrentDictionary<long, SemaphoreSlim> _refreshLocks = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Build the initial snapshots before the web host begins serving availability requests.
        await RefreshAllAsync(cancellationToken);
        logger.LogInformation("Loaded hotel inventory cache for {HotelCount} hotels.", _hotels.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool TryGet(long hotelId, out CachedHotelInventory? inventory) =>
        _hotels.TryGetValue(hotelId, out inventory);

    public async Task RefreshHotelAsync(long hotelId, CancellationToken cancellationToken)
    {
        // Prevent overlapping refreshes from publishing an older snapshot after a newer one.
        var refreshLock = _refreshLocks.GetOrAdd(hotelId, _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AltairisDbContext>();
            var hotel = await dbContext.Hotels
                .AsNoTracking()
                .Where(item => item.Id == hotelId)
                .Select(item => new HotelRow(item.Id, item.Active ?? false))
                .FirstOrDefaultAsync(cancellationToken);
            if (hotel is null)
            {
                _hotels.TryRemove(hotelId, out _);
                return;
            }

            var rooms = await LoadRoomsAsync(dbContext, hotelId, cancellationToken);
            var roomIds = rooms.Select(room => room.Id).ToArray();
            var inventoryRows = await LoadInventoryAsync(dbContext, roomIds, cancellationToken);
            var stays = await LoadBookedStaysAsync(dbContext, roomIds, cancellationToken);
            _hotels[hotelId] = BuildSnapshot(hotel, rooms, inventoryRows, stays);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AltairisDbContext>();
        var hotels = await dbContext.Hotels
            .AsNoTracking()
            .Select(item => new HotelRow(item.Id, item.Active ?? false))
            .ToListAsync(cancellationToken);
        var rooms = await LoadRoomsAsync(dbContext, null, cancellationToken);
        var roomIds = rooms.Select(room => room.Id).ToArray();
        var inventoryRows = await LoadInventoryAsync(dbContext, roomIds, cancellationToken);
        var stays = await LoadBookedStaysAsync(dbContext, roomIds, cancellationToken);

        _hotels.Clear();
        foreach (var hotel in hotels)
        {
            _hotels[hotel.Id] = BuildSnapshot(
                hotel,
                rooms.Where(room => room.HotelId == hotel.Id).ToList(),
                inventoryRows,
                stays);
        }
    }

    private static Task<List<RoomRow>> LoadRoomsAsync(
        AltairisDbContext dbContext,
        long? hotelId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Rooms.AsNoTracking().AsQueryable();
        if (hotelId.HasValue)
        {
            query = query.Where(room => room.HotelId == hotelId.Value);
        }

        return query
            .Select(room => new RoomRow(
                room.Id,
                room.HotelId,
                room.Name,
                room.Description,
                room.Price))
            .ToListAsync(cancellationToken);
    }

    private static Task<List<InventoryRow>> LoadInventoryAsync(
        AltairisDbContext dbContext,
        IReadOnlyCollection<long> roomIds,
        CancellationToken cancellationToken) =>
        roomIds.Count == 0
            ? Task.FromResult(new List<InventoryRow>())
            : dbContext.RoomInventories
                .AsNoTracking()
                .Where(item => roomIds.Contains(item.RoomId))
                .Select(item => new InventoryRow(
                    item.RoomId,
                    item.Date,
                    item.Capacity,
                    item.FreeSale))
                .ToListAsync(cancellationToken);

    private static Task<List<BookedStayRow>> LoadBookedStaysAsync(
        AltairisDbContext dbContext,
        IReadOnlyCollection<long> roomIds,
        CancellationToken cancellationToken) =>
        roomIds.Count == 0
            ? Task.FromResult(new List<BookedStayRow>())
            : dbContext.BookedRooms
                .AsNoTracking()
                .Where(item => item.RoomId != null &&
                    roomIds.Contains(item.RoomId.Value) &&
                    item.Booking.Status == "booked" &&
                    item.Booking.CheckIn != null &&
                    item.Booking.CheckOut != null)
                .Select(item => new BookedStayRow(
                    item.RoomId!.Value,
                    item.Booking.CheckIn!.Value,
                    item.Booking.CheckOut!.Value))
                .ToListAsync(cancellationToken);

    private static CachedHotelInventory BuildSnapshot(
        HotelRow hotel,
        IReadOnlyCollection<RoomRow> rooms,
        IReadOnlyCollection<InventoryRow> inventoryRows,
        IReadOnlyCollection<BookedStayRow> bookedStays)
    {
        var roomIds = rooms.Select(room => room.Id).ToHashSet();
        var inventoryByRoom = inventoryRows
            .Where(item => roomIds.Contains(item.RoomId))
            .GroupBy(item => item.RoomId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(item => item.Date));
        var usage = new Dictionary<(long RoomId, DateOnly Date), int>();

        foreach (var stay in bookedStays)
        {
            if (!roomIds.Contains(stay.RoomId) ||
                !inventoryByRoom.TryGetValue(stay.RoomId, out var roomInventory))
            {
                continue;
            }

            var firstNight = DateOnly.FromDateTime(stay.CheckIn);
            var lastNight = DateOnly.FromDateTime(stay.CheckOut);
            for (var date = firstNight; date < lastNight; date = date.AddDays(1))
            {
                if (roomInventory.ContainsKey(date))
                {
                    var key = (stay.RoomId, date);
                    usage[key] = usage.GetValueOrDefault(key) + 1;
                }
            }
        }

        var cachedRooms = rooms
            .OrderBy(room => room.Name)
            .Select(room => new CachedRoom(
                room.Id,
                room.Name,
                room.Description,
                room.Price,
                inventoryByRoom.TryGetValue(room.Id, out var roomInventory)
                    ? roomInventory.ToDictionary(
                        pair => pair.Key,
                        item => new CachedRoomNight(
                            item.Value.Capacity,
                            item.Value.FreeSale,
                            usage.GetValueOrDefault((room.Id, item.Key))))
                    : new Dictionary<DateOnly, CachedRoomNight>()))
            .ToList();

        return new CachedHotelInventory(hotel.Id, hotel.IsActive, cachedRooms);
    }

    private sealed record HotelRow(long Id, bool IsActive);

    private sealed record RoomRow(
        long Id,
        long HotelId,
        string Name,
        string? Description,
        double Price);

    private sealed record InventoryRow(
        long RoomId,
        DateOnly Date,
        int Capacity,
        bool FreeSale);

    private sealed record BookedStayRow(long RoomId, DateTime CheckIn, DateTime CheckOut);
}
