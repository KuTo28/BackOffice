namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Allotment;

/// <summary>Values used to create an allotment for room occupancy and free-sale rules.</summary>
public class AllotmentCreateCommand
{
    /// <summary>The allotment quantity, when sales are limited.</summary>
    public int? Value { get; set; }

    /// <summary>The maximum number of adults covered by the allotment.</summary>
    public int NumAdults { get; set; }

    /// <summary>The maximum number of children covered by the allotment.</summary>
    public int NumChildren { get; set; }

    /// <summary>The maximum number of babies covered by the allotment.</summary>
    public int NumBabies { get; set; }

    /// <summary>Whether the room may be sold without a fixed allotment quantity.</summary>
    public bool FreeSale { get; set; }
}