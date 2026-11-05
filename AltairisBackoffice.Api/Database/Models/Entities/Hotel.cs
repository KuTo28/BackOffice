using System;
using System.Collections.Generic;

namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Hotel profile and navigation relationships stored by the backoffice.</summary>
public partial class Hotel
{
    /// <summary>Database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Hotel display name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Hotel contact email.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Hotel contact phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Whether the hotel is active for sales.</summary>
    public bool? Active { get; set; }

    /// <summary>Street address.</summary>
    public string? Address { get; set; }

    /// <summary>City where the hotel is located.</summary>
    public string? City { get; set; }

    /// <summary>Country where the hotel is located.</summary>
    public string? Country { get; set; }

    /// <summary>Short hotel description.</summary>
    public string? Description { get; set; }

    /// <summary>Creation timestamp stored by the database.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Bookings associated with this hotel.</summary>
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    /// <summary>Room types available at this hotel.</summary>
    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
