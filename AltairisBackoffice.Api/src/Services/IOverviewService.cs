using AltairisBackoffice.Api.src.Infrastructure.Dtos.Overview;

namespace AltairisBackoffice.Api.src.Services;

/// <summary>Builds booking and daily operations summaries for the overview screens.</summary>
public interface IOverviewService
{
    /// <summary>Gets overall operational counts and recent booking activity.</summary>
    Task<OverviewResponse> GetOverviewAsync(
        DateOnly date,
        DateOnly? trendDate,
        CancellationToken cancellationToken);
    /// <summary>Gets operational counts and recent booking activity for one hotel.</summary>
    Task<OverviewResponse?> GetHotelOverviewAsync(
        long hotelId,
        DateOnly date,
        DateOnly? trendDate,
        CancellationToken cancellationToken);
    /// <summary>Gets arrival, departure, booked, and canceled counts for one date.</summary>
    Task<OverviewSummaryResponse> GetSummaryAsync(DateOnly date, CancellationToken cancellationToken);
}
