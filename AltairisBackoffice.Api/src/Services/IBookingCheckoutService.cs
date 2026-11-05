using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Outcome of a booking checkout operation.</summary>
public enum BookingCheckoutStatus
{
    Success,
    AlreadyConfirmed,
    InvalidStay,
    HotelNotFound,
    HotelInactive,
    InvalidRoomSelection,
    Unavailable,
    QuoteNotFound,
    QuoteExpired,
    ConcurrentUpdate
}

/// <summary>Implements the availability, quote, and confirmation steps of booking checkout.</summary>
public interface IBookingCheckoutService
{
    /// <summary>Reads the cached inventory snapshot for a hotel stay.</summary>
    Task<(BookingCheckoutStatus Status, BookingAvailabilityResponse? Response)> GetAvailabilityAsync(
        long hotelId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken);

    /// <summary>Revalidates selected room types against the database and stores an expiring quote in memory.</summary>
    Task<(BookingCheckoutStatus Status, BookingQuoteResponse? Response)> CreateQuoteAsync(
        CreateBookingQuoteRequest request,
        Guid quoteId,
        CancellationToken cancellationToken);

    /// <summary>Retrieves a quote while it is valid or after it has already been confirmed.</summary>
    Task<(BookingCheckoutStatus Status, BookingQuoteResponse? Response)> GetQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken);

    /// <summary>Revalidates and confirms a quote, creating at most one booking for that quote.</summary>
    Task<(BookingCheckoutStatus Status, ConfirmedBookingResponse? Response)> ConfirmAsync(
        Guid quoteId,
        ConfirmBookingQuoteRequest request,
        CancellationToken cancellationToken);
}
