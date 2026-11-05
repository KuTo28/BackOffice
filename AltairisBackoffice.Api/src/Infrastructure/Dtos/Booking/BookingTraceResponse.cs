using System.Text.Json;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

/// <summary>One request or response trace associated with a booking.</summary>
/// <param name="Id">The trace record identifier.</param>
/// <param name="BookingId">The booking identifier, when the trace has been linked to a booking.</param>
/// <param name="ParentId">The shared quote/confirm process identifier; cancellation traces do not use one.</param>
/// <param name="Operation">The operation name, such as <c>quote</c>, <c>confirm</c>, or <c>cancel</c>.</param>
/// <param name="Direction">Whether the payload is a <c>request</c> or <c>response</c>.</param>
/// <param name="StatusCode">The HTTP status code for a response trace, when applicable.</param>
/// <param name="Payload">The serialized request or response body.</param>
/// <param name="CreatedAt">The UTC time when the trace was recorded.</param>
public sealed record BookingTraceResponse(
    long Id,
    long? BookingId,
    Guid? ParentId,
    string Operation,
    string Direction,
    int? StatusCode,
    JsonElement Payload,
    DateTimeOffset CreatedAt);
