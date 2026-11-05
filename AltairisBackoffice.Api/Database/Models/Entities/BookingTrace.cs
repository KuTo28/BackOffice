namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Persisted request or response payload for a booking workflow operation.</summary>
public sealed class BookingTrace
{
    /// <summary>Database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Booking associated with the trace, once known.</summary>
    public long? BookingId { get; set; }

    /// <summary>Shared identifier linking quote and confirmation traces for one checkout process.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Workflow operation name, such as quote, confirm, or cancel.</summary>
    public string Operation { get; set; } = null!;

    /// <summary>Whether the payload is a request or a response.</summary>
    public string Direction { get; set; } = null!;

    /// <summary>HTTP response status code; null for request traces.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Serialized request or response payload.</summary>
    public string Payload { get; set; } = null!;

    /// <summary>UTC time when the trace was recorded.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
