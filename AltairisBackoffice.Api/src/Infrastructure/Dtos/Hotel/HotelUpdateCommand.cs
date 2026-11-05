using System.ComponentModel.DataAnnotations;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Hotel;

/// <summary>Optional hotel fields to update; omitted values remain unchanged.</summary>
public class HotelUpdateCommand
{
    /// <summary>The hotel's display name.</summary>
    [StringLength(255)]
    public string? Name { get; set; }

    /// <summary>The hotel's contact email address.</summary>
    [EmailAddress, StringLength(255)]
    public string? Email { get; set; }

    /// <summary>The hotel's contact phone number.</summary>
    [StringLength(255)]
    public string? Phone { get; set; }

    /// <summary>Whether the hotel can be used for new sales.</summary>
    public bool? Active { get; set; }

    /// <summary>The hotel's street address.</summary>
    [StringLength(255)]
    public string? Address { get; set; }

    /// <summary>The city where the hotel is located.</summary>
    [StringLength(255)]
    public string? City { get; set; }

    /// <summary>The country where the hotel is located.</summary>
    [StringLength(255)]
    public string? Country { get; set; }

    /// <summary>A short description displayed in the backoffice.</summary>
    [StringLength(255)]
    public string? Description { get; set; }
}