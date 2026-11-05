using System.ComponentModel.DataAnnotations;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Hotel;

/// <summary>Required and optional details for creating a hotel.</summary>
public class HotelCreateCommand
{
    /// <summary>The hotel's display name.</summary>
    [Required, StringLength(255)]
    public required string Name { get; set; }

    /// <summary>The hotel's contact email address.</summary>
    [Required, EmailAddress, StringLength(255)]
    public required string Email { get; set; }

    /// <summary>The hotel's contact phone number.</summary>
    [StringLength(255)]
    public required string Phone { get; set; }

    /// <summary>Whether the hotel can be used for new sales.</summary>
    public bool Active { get; set; }

    /// <summary>The hotel's street address.</summary>
    [StringLength(255)]
    public required string Address { get; set; }

    /// <summary>The city where the hotel is located.</summary>
    [StringLength(255)]
    public required string City { get; set; }

    /// <summary>The country where the hotel is located.</summary>
    [StringLength(255)]
    public required string Country { get; set; }

    /// <summary>A short description displayed in the backoffice.</summary>
    [StringLength(255)]
    public required string Description { get; set; }
}