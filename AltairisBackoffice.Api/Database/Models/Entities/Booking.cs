using System;
using System.Collections.Generic;

namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Confirmed hotel reservation and its primary guest and stay details.</summary>
public partial class Booking
{
    /// <summary>Database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Identifier of the hotel where the stay is booked.</summary>
    public long HotelId { get; set; }

    /// <summary>Booking creation timestamp.</summary>
    public DateTime? BookingDate { get; set; }

    /// <summary>First night of the stay.</summary>
    public DateTime? CheckIn { get; set; }

    /// <summary>Checkout date, excluded from the stay nights.</summary>
    public DateTime? CheckOut { get; set; }

    /// <summary>Booking status, currently <c>booked</c> or <c>canceled</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Total price captured at confirmation.</summary>
    public double? TotalPrice { get; set; }

    /// <summary>Primary guest name.</summary>
    public string? GuestName { get; set; }

    /// <summary>Primary guest phone number.</summary>
    public string? GuestPhone { get; set; }

    /// <summary>Primary guest email address.</summary>
    public string? GuestEmail { get; set; }

    /// <summary>Hotel associated with this booking.</summary>
    public virtual Hotel Hotel { get; set; } = null!;
}
