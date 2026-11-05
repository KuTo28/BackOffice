namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

/// <summary>Represents room-type availability for a hotel stay.</summary>
/// <param name="HotelId">The hotel searched.</param>
/// <param name="CheckIn">The first night of the stay.</param>
/// <param name="CheckOut">The checkout date, excluded from the stay nights.</param>
/// <param name="Nights">The number of nights in the requested stay.</param>
/// <param name="Rooms">Availability and pricing for each room type at the hotel.</param>
public sealed record BookingAvailabilityResponse(
    long HotelId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    IReadOnlyList<AvailableRoomResponse> Rooms);

/// <summary>Represents one room type and its availability during the requested stay.</summary>
/// <param name="RoomId">The room type identifier.</param>
/// <param name="Name">The room type's display name.</param>
/// <param name="Description">A description of the room type, when available.</param>
/// <param name="PricePerNight">The price for one night.</param>
/// <param name="IsAvailable">Whether the room type can be booked for all requested nights.</param>
/// <param name="AvailableUnits">The minimum remaining limited capacity across the stay, or null for free sale.</param>
/// <param name="FreeSale">Whether any night in the stay uses free-sale inventory.</param>
public sealed record AvailableRoomResponse(
    long RoomId,
    string Name,
    string? Description,
    double PricePerNight,
    bool IsAvailable,
    int? AvailableUnits,
    bool FreeSale);
