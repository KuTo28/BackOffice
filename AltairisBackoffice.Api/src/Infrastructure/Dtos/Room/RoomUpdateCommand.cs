using System.ComponentModel.DataAnnotations;

namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Room;

/// <summary>Optional room type fields to update; omitted values remain unchanged.</summary>
public class RoomUpdateCommand
{
    /// <summary>The room type's display name.</summary>
    [StringLength(255)]
    public string? Name { get; set; }

    /// <summary>A short description of the room type.</summary>
    [StringLength(255)]
    public string? Description { get; set; }

    /// <summary>The price per night for this room type.</summary>
    [Range(0, double.MaxValue)]
    public double? Price { get; set; }
}