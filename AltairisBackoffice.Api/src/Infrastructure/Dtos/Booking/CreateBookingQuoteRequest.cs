using System.ComponentModel.DataAnnotations;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

/// <summary>Payload for validating and quoting a hotel stay.</summary>
public sealed record CreateBookingQuoteRequest
{
    /// <summary>The hotel identifier.</summary>
    [Range(1, long.MaxValue)]
    public required long HotelId { get; init; }

    /// <summary>The check-in date.</summary>
    public required DateOnly CheckIn { get; init; }

    /// <summary>The check-out date.</summary>
    public required DateOnly CheckOut { get; init; }

    /// <summary>The selected room type identifiers.</summary>
    [Required, MinLength(1)]
    public required IReadOnlyList<long> RoomIds { get; init; }
}
