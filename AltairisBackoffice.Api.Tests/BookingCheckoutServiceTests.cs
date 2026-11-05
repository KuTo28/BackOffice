using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;
using AltairisBackoffice.Api.src.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AltairisBackoffice.Api.Tests;

public sealed class BookingCheckoutServiceTests
{
    private static readonly DateOnly CheckIn = new(2026, 11, 10);
    private static readonly DateOnly CheckOut = new(2026, 11, 12);

    [Fact]
    public async Task GetAvailabilityAsync_RejectsInvalidStayWithoutLookingUpHotel()
    {
        var cache = new FakeHotelInventoryCache();
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckIn, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.InvalidStay, result.Status);
        Assert.Null(result.Response);
        Assert.Empty(cache.LookedUpHotelIds);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReturnsNotFoundWhenHotelIsNotCached()
    {
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new FakeHotelInventoryCache());

        var result = await service.GetAvailabilityAsync(42, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.HotelNotFound, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task GetAvailabilityAsync_RejectsInactiveHotel()
    {
        var cache = new FakeHotelInventoryCache(CreateHotel(isActive: false));
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.HotelInactive, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task GetAvailabilityAsync_UsesMinimumLimitedCapacityAcrossStayNights()
    {
        var room = CreateRoom(
            id: 11,
            ("2026-11-10", new CachedRoomNight(5, false, 2)),
            ("2026-11-11", new CachedRoomNight(4, false, 3)),
            // Checkout day is not a night in the stay and must not affect availability.
            ("2026-11-12", new CachedRoomNight(1, false, 1)));
        var cache = new FakeHotelInventoryCache(CreateHotel(room));
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(2, result.Response.Nights);
        var availableRoom = Assert.Single(result.Response.Rooms);
        Assert.True(availableRoom.IsAvailable);
        Assert.Equal(1, availableRoom.AvailableUnits);
        Assert.False(availableRoom.FreeSale);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsFreeSaleWithoutFiniteUnitLimit()
    {
        var room = CreateRoom(
            id: 11,
            ("2026-11-10", new CachedRoomNight(2, true, 20)),
            ("2026-11-11", new CachedRoomNight(2, true, 20)));
        var cache = new FakeHotelInventoryCache(CreateHotel(room));
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.Success, result.Status);
        var availableRoom = Assert.Single(Assert.IsType<BookingAvailabilityResponse>(result.Response).Rooms);
        Assert.True(availableRoom.IsAvailable);
        Assert.Null(availableRoom.AvailableUnits);
        Assert.True(availableRoom.FreeSale);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ReportsLimitedUnitsForStayMixingFreeSaleAndLimitedNights()
    {
        var room = CreateRoom(
            id: 11,
            ("2026-11-10", new CachedRoomNight(5, false, 3)),
            ("2026-11-11", new CachedRoomNight(1, true, 10)));
        var cache = new FakeHotelInventoryCache(CreateHotel(room));
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.Success, result.Status);
        var availableRoom = Assert.Single(Assert.IsType<BookingAvailabilityResponse>(result.Response).Rooms);
        Assert.True(availableRoom.IsAvailable);
        Assert.Equal(2, availableRoom.AvailableUnits);
        Assert.True(availableRoom.FreeSale);
    }

    [Fact]
    public async Task GetAvailabilityAsync_MarksRoomUnavailableWhenAnyStayNightIsUnconfigured()
    {
        var room = CreateRoom(
            id: 11,
            ("2026-11-10", new CachedRoomNight(5, false, 0)));
        var cache = new FakeHotelInventoryCache(CreateHotel(room));
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.Success, result.Status);
        var availableRoom = Assert.Single(Assert.IsType<BookingAvailabilityResponse>(result.Response).Rooms);
        Assert.False(availableRoom.IsAvailable);
        Assert.Equal(0, availableRoom.AvailableUnits);
    }

    [Fact]
    public async Task GetAvailabilityAsync_MarksRoomUnavailableWhenLimitedInventoryIsFullyBooked()
    {
        var room = CreateRoom(
            id: 11,
            ("2026-11-10", new CachedRoomNight(2, false, 2)),
            ("2026-11-11", new CachedRoomNight(5, false, 1)));
        var cache = new FakeHotelInventoryCache(CreateHotel(room));
        using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, cache);

        var result = await service.GetAvailabilityAsync(1, CheckIn, CheckOut, CancellationToken.None);

        Assert.Equal(BookingCheckoutStatus.Success, result.Status);
        var availableRoom = Assert.Single(Assert.IsType<BookingAvailabilityResponse>(result.Response).Rooms);
        Assert.False(availableRoom.IsAvailable);
        Assert.Equal(0, availableRoom.AvailableUnits);
    }

    private static BookingCheckoutService CreateService(
        AltairisDbContext dbContext,
        IHotelInventoryCache inventoryCache) =>
        new(
            dbContext,
            inventoryCache,
            new BookingQuoteStore(NullLogger<BookingQuoteStore>.Instance),
            NullLogger<BookingCheckoutService>.Instance);

    private static AltairisDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AltairisDbContext>()
            .UseNpgsql("Host=localhost;Database=availability-tests;Username=test;Password=test")
            .Options;
        return new AltairisDbContext(options);
    }

    private static CachedHotelInventory CreateHotel(params CachedRoom[] rooms) =>
        CreateHotel(isActive: true, rooms);

    private static CachedHotelInventory CreateHotel(bool isActive, params CachedRoom[] rooms) =>
        new(1, isActive, rooms);

    private static CachedRoom CreateRoom(
        long id,
        params (string Date, CachedRoomNight Night)[] nights) =>
        new(
            id,
            "Standard Double",
            "A standard room",
            120,
            nights.ToDictionary(
                item => DateOnly.Parse(item.Date),
                item => item.Night));

    private sealed class FakeHotelInventoryCache(params CachedHotelInventory[] hotels) : IHotelInventoryCache
    {
        private readonly Dictionary<long, CachedHotelInventory> _hotels =
            hotels.ToDictionary(hotel => hotel.HotelId);

        public List<long> LookedUpHotelIds { get; } = [];

        public bool TryGet(long hotelId, out CachedHotelInventory? inventory)
        {
            LookedUpHotelIds.Add(hotelId);
            return _hotels.TryGetValue(hotelId, out inventory);
        }

        public Task RefreshHotelAsync(long hotelId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
