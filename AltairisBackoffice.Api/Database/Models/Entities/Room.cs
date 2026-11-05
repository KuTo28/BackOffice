using System;
using System.Collections.Generic;

namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Hotel room type, including its price and inventory configuration.</summary>
public partial class Room
{
    /// <summary>Database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Room type display name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Room type description.</summary>
    public string? Description { get; set; }

    /// <summary>Price per night.</summary>
    public double Price { get; set; }

    /// <summary>Identifier of the hotel that owns this room type.</summary>
    public long HotelId { get; set; }

    /// <summary>Optional occupancy and free-sale allotment assigned to this room type.</summary>
    public long? AllotmentId { get; set; }

    /// <summary>Allotment assigned to this room type, when configured.</summary>
    public virtual Allotment? Allotment { get; set; }

    /// <summary>Hotel that owns this room type.</summary>
    public virtual Hotel Hotel { get; set; } = null!;
}
