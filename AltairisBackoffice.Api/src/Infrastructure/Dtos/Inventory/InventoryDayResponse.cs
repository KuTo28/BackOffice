namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;

/// <summary>Availability for one room type on one calendar day.</summary>
/// <param name="RoomId">The room type identifier.</param>
/// <param name="RoomName">The room type's display name.</param>
/// <param name="Date">The calendar date represented by the result.</param>
/// <param name="Capacity">The configured capacity for the date.</param>
/// <param name="Booked">The number of booked room units for the date.</param>
/// <param name="Available">Remaining capacity, or null when free sale applies.</param>
/// <param name="FreeSale">Whether the room type is sold under free-sale rules.</param>
public sealed record InventoryDayResponse(
    long RoomId,
    string RoomName,
    DateOnly Date,
    int Capacity,
    int Booked,
    int? Available,
    bool FreeSale);
