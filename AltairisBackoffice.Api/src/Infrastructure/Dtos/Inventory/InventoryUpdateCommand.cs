namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;

/// <summary>Inventory settings to apply to every date in an inclusive range.</summary>
public class InventoryUpdateCommand
{
    /// <summary>The first date to update.</summary>
    public required DateOnly From { get; set; }

    /// <summary>The last date to update.</summary>
    public required DateOnly To { get; set; }

    /// <summary>The configured room capacity per night.</summary>
    public required int Capacity { get; set; }

    /// <summary>Whether the room may be sold without a fixed capacity limit.</summary>
    public bool FreeSale { get; set; }
}
