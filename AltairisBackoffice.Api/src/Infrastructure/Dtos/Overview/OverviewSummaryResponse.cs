namespace AltairisBackoffice.Api.src.Infrastructure.Dtos.Overview;

/// <summary>Operational booking counts for a business date.</summary>
public sealed record OverviewSummaryResponse(int Arrivals, int Departures, int Booked, int Canceled);
