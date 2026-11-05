namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

/// <summary>Represents a validated, expiring booking quote.</summary>
/// <param name="QuoteId">The identifier used to retrieve and confirm this quote.</param>
/// <param name="HotelId">The hotel for the quoted stay.</param>
/// <param name="CheckIn">The first night of the stay.</param>
/// <param name="CheckOut">The checkout date, excluded from the stay nights.</param>
/// <param name="Nights">The number of nights in the stay.</param>
/// <param name="TotalPrice">The total price for all selected room types and nights.</param>
/// <param name="ExpiresAt">The UTC time after which an unconfirmed quote cannot be used.</param>
/// <param name="Rooms">The selected room types and their quoted prices.</param>
public sealed record BookingQuoteResponse(
    Guid QuoteId,
    long HotelId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    double TotalPrice,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<BookingQuoteRoomResponse> Rooms);

/// <summary>Represents a room type and its quoted price.</summary>
/// <param name="RoomId">The room type identifier.</param>
/// <param name="Name">The room type's display name at quote creation.</param>
/// <param name="Description">The room type description at quote creation, when available.</param>
/// <param name="PricePerNight">The nightly price captured by the quote.</param>
/// <param name="TotalPrice">The price for this room type over the quoted stay.</param>
public sealed record BookingQuoteRoomResponse(
    long RoomId,
    string Name,
    string? Description,
    double PricePerNight,
    double TotalPrice);

/// <summary>Represents the booking created from a confirmed quote.</summary>
/// <param name="BookingId">The persisted booking identifier.</param>
/// <param name="QuoteId">The quote that produced the booking.</param>
/// <param name="Status">The resulting booking status.</param>
/// <param name="TotalPrice">The total price captured by the quote.</param>
/// <param name="AlreadyConfirmed">Whether this response represents a previously confirmed quote.</param>
public sealed record ConfirmedBookingResponse(
    long BookingId,
    Guid QuoteId,
    string Status,
    double TotalPrice,
    bool AlreadyConfirmed);
