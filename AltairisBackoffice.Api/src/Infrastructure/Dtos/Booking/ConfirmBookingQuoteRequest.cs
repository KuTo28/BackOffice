using System.ComponentModel.DataAnnotations;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Booking;

/// <summary>Guest details required to confirm a booking quote.</summary>
public sealed record ConfirmBookingQuoteRequest
{
    /// <summary>The guest's full name.</summary>
    [Required, StringLength(255)]
    public required string GuestName { get; init; }

    /// <summary>The guest's phone number.</summary>
    [Required, StringLength(255)]
    public required string GuestPhone { get; init; }

    /// <summary>The guest's email address.</summary>
    [Required, EmailAddress, StringLength(255)]
    public required string GuestEmail { get; init; }
}
