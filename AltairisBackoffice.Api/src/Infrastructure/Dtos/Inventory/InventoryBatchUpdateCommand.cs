namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;

/// <summary>Payload for applying one inventory range to multiple room types.</summary>
public sealed record InventoryBatchUpdateCommand
{
    /// <summary>The room type identifiers to update.</summary>
    public required IReadOnlyList<long> RoomIds { get; init; }

    /// <summary>The first date to update.</summary>
    public required DateOnly From { get; init; }

    /// <summary>The last date to update.</summary>
    public required DateOnly To { get; init; }

    /// <summary>The configured room capacity.</summary>
    public required int Capacity { get; init; }

    /// <summary>Whether sales are unlimited.</summary>
    public bool FreeSale { get; init; }
}
