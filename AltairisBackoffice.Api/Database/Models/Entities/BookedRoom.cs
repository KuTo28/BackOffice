using System;
using System.Collections.Generic;

namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Room type snapshot captured for a booking at confirmation time.</summary>
public partial class BookedRoom
{
    /// <summary>Database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Identifier of the booking that contains this room.</summary>
    public long BookingId { get; set; }

    /// <summary>Room type identifier, when the original room type still exists.</summary>
    public long? RoomId { get; set; }

    /// <summary>Nightly room price captured when the booking was confirmed.</summary>
    public double Price { get; set; }

    /// <summary>Room type name captured when the booking was confirmed.</summary>
    public string? Name { get; set; }

    /// <summary>Room type description captured when the booking was confirmed.</summary>
    public string? Description { get; set; }

    /// <summary>Parent booking.</summary>
    public virtual Booking Booking { get; set; } = null!;
}
