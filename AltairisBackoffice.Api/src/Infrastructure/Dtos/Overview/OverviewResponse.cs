namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Overview;

/// <summary>Daily operations summary and recent bookings for the backoffice overview.</summary>
/// <param name="Arrivals">Number of active bookings checking in on the requested business date.</param>
/// <param name="Departures">Number of active bookings checking out on the requested business date.</param>
/// <param name="Booked">Total number of active bookings in the selected scope.</param>
/// <param name="Canceled">Total number of canceled bookings in the selected scope.</param>
/// <param name="BookingsPerDay">Booking creation counts for the seven-day trend interval.</param>
/// <param name="RecentBookings">The six most recently created bookings in the selected scope.</param>
public sealed record OverviewResponse(
    int Arrivals,
    int Departures,
    int Booked,
    int Canceled,
    IReadOnlyList<OverviewBookingDayResponse> BookingsPerDay,
    IReadOnlyList<OverviewBookingResponse> RecentBookings);

/// <summary>Booking creation count for a calendar day.</summary>
/// <param name="Date">The calendar date.</param>
/// <param name="Count">Number of bookings created on the date.</param>
public sealed record OverviewBookingDayResponse(DateOnly Date, int Count);

/// <summary>A recent booking with the information shown on the overview page.</summary>
/// <param name="Id">The booking identifier.</param>
/// <param name="HotelId">The hotel identifier.</param>
/// <param name="HotelName">The hotel's display name.</param>
/// <param name="BookingDate">The creation date, when available.</param>
/// <param name="CheckIn">The first night of the stay.</param>
/// <param name="CheckOut">The checkout date.</param>
/// <param name="Status">The booking status.</param>
/// <param name="TotalPrice">The total booking price.</param>
/// <param name="GuestName">The primary guest's name.</param>
/// <param name="GuestPhone">The primary guest's phone number.</param>
/// <param name="GuestEmail">The primary guest's email address.</param>
/// <param name="Rooms">The room type snapshots stored with the booking.</param>
public sealed record OverviewBookingResponse(
    long Id,
    long HotelId,
    string HotelName,
    DateTime? BookingDate,
    DateTime? CheckIn,
    DateTime? CheckOut,
    string? Status,
    double? TotalPrice,
    string? GuestName,
    string? GuestPhone,
    string? GuestEmail,
    IReadOnlyList<OverviewRoomResponse> Rooms);

/// <summary>A room snapshot belonging to a recent booking.</summary>
/// <param name="RoomId">The room type identifier, when available.</param>
/// <param name="Name">The room type name stored with the booking.</param>
/// <param name="Price">The nightly price stored with the booking.</param>
public sealed record OverviewRoomResponse(long? RoomId, string? Name, double Price);
