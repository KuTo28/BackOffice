using System.ComponentModel.DataAnnotations;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Room;

/// <summary>Values used to create a room type within a hotel.</summary>
public class RoomCreateCommand
{
    /// <summary>The room type's display name.</summary>
    [Required, StringLength(255)]
    public required string Name { get; set; }

    /// <summary>A short description of the room type.</summary>
    [StringLength(255)]
    public required string Description { get; set; }

    /// <summary>The price per night for this room type.</summary>
    [Range(0, double.MaxValue)]
    public double Price { get; set; }

    /// <summary>The hotel that owns this room type.</summary>
    [Range(1, long.MaxValue)]
    public long HotelId { get; set; }
}