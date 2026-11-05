namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;

/// <summary>Aggregated room inventory for one calendar day.</summary>
/// <param name="Date">The calendar date represented by the result.</param>
/// <param name="Capacity">Total limited capacity, or null when free sale prevents a finite total.</param>
/// <param name="ConfiguredCapacity">Sum of capacities configured for the date.</param>
/// <param name="Available">Remaining limited inventory, or null when no finite total applies.</param>
/// <param name="Used">Booked room units or, for a finite total, consumed capacity.</param>
/// <param name="Configured">Whether any room inventory is configured for this date.</param>
/// <param name="Incomplete">Whether only some hotel room types have inventory configured.</param>
/// <param name="IncludesFreeSale">Whether any included room type uses free sale.</param>
/// <param name="Status">Aggregate status: <c>available</c>, <c>limited</c>, or <c>unavailable</c>.</param>
/// <param name="Rooms">Per-room inventory details for the date.</param>
public sealed record InventoryCalendarDayResponse(
    DateOnly Date,
    int? Capacity,
    int? ConfiguredCapacity,
    int? Available,
    int Used,
    bool Configured,
    bool Incomplete,
    bool IncludesFreeSale,
    string Status,
    IReadOnlyList<InventoryRoomCalendarResponse> Rooms);

/// <summary>Availability and usage for one room type on a calendar day.</summary>
/// <param name="RoomId">The room type identifier.</param>
/// <param name="RoomName">The room type's display name.</param>
/// <param name="ConfiguredCapacity">The configured capacity, or null when no finite capacity applies.</param>
/// <param name="Available">Remaining capacity, or null when it cannot be represented as a finite value.</param>
/// <param name="Used">Number of booked room units on this date.</param>
/// <param name="Configured">Whether inventory is configured for this room type and date.</param>
/// <param name="FreeSale">Whether the room type is sold under free-sale rules.</param>
public sealed record InventoryRoomCalendarResponse(
    long RoomId,
    string RoomName,
    int? ConfiguredCapacity,
    int? Available,
    int Used,
    bool Configured,
    bool FreeSale);
