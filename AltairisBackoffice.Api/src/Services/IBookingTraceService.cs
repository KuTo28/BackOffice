using AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Persists and retrieves booking checkout and cancellation traces.</summary>
public interface IBookingTraceService
{
    /// <summary>Stores an operation's request or response payload.</summary>
    Task RecordAsync(
        long? bookingId,
        Guid? parentId,
        string operation,
        string direction,
        int? statusCode,
        object payload,
        CancellationToken cancellationToken);

    /// <summary>Associates quote and confirmation traces with the booking created by the process.</summary>
    Task LinkProcessToBookingAsync(
        Guid parentId,
        long bookingId,
        CancellationToken cancellationToken);

    /// <summary>Finds the booking already associated with a quote/confirmation process.</summary>
    Task<long?> GetLinkedBookingIdAsync(
        Guid parentId,
        CancellationToken cancellationToken);

    /// <summary>Gets a booking's traces in chronological order, or null if the booking does not exist.</summary>
    Task<IReadOnlyList<BookingTraceResponse>?> GetByBookingIdAsync(
        long bookingId,
        CancellationToken cancellationToken);
}
