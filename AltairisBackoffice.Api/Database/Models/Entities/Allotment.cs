using System;
using System.Collections.Generic;

namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Room occupancy limits and free-sale settings associated with room types.</summary>
public partial class Allotment
{
    /// <summary>Database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Limited inventory quantity; null when no quantity is configured.</summary>
    public int? Value { get; set; }

    /// <summary>Maximum adult occupancy.</summary>
    public int NumAdults { get; set; }

    /// <summary>Maximum child occupancy.</summary>
    public int NumChildren { get; set; }

    /// <summary>Maximum baby occupancy.</summary>
    public int NumBabies { get; set; }

    /// <summary>Whether sales may exceed the configured allotment quantity.</summary>
    public bool FreeSale { get; set; }

    /// <summary>Room types using this allotment.</summary>
    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
