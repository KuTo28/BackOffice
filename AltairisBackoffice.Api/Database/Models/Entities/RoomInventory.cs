namespace AltairisBackoffice.Api.Database.Models.Entities;

/// <summary>Capacity and free-sale configuration for a room type on one calendar night.</summary>
public class RoomInventory
{
    /// <summary>Room type identifier; part of the composite primary key.</summary>
    public long RoomId { get; set; }

    /// <summary>Calendar night; part of the composite primary key.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Configured number of rooms for this night.</summary>
    public int Capacity { get; set; }

    /// <summary>Whether availability is not capped by the configured capacity.</summary>
    public bool FreeSale { get; set; }

    /// <summary>Room type to which the dated inventory belongs.</summary>
    public virtual Room Room { get; set; } = null!;
}
