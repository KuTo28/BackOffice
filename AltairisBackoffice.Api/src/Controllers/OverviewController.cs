using AltairisBackoffice.Api.src.Infrastructure.Dtos.Overview;
using AltairisBackoffice.Api.src.Services;
using Microsoft.AspNetCore.Mvc;

namespace AltairisBackoffice.Api.src.Controllers;

/// <summary>Provides aggregated data for the backoffice overview.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class OverviewController(IOverviewService overviewService) : ControllerBase
{
    /// <summary>Gets operational counts and recent bookings.</summary>
    /// <param name="date">The business date used for arrival and departure counts.</param>
    /// <param name="trendDate">The final day of the seven-day booking trend; defaults to <paramref name="date"/>.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The overview data required by the dashboard.</returns>
    /// <response code="200">The overview data was returned.</response>
    [HttpGet]
    [ProducesResponseType(typeof(OverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OverviewResponse>> GetOverview(
        [FromQuery] DateOnly date,
        [FromQuery] DateOnly? trendDate,
        CancellationToken cancellationToken)
    {
        var overview = await overviewService.GetOverviewAsync(date, trendDate, cancellationToken);
        return Ok(overview);
    }

    /// <summary>Gets operational counts and recent bookings for one hotel.</summary>
    /// <param name="hotelId">The hotel identifier.</param>
    /// <param name="date">The business date used for arrival and departure counts.</param>
    /// <param name="trendDate">The final day of the seven-day booking trend; defaults to <paramref name="date"/>.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The hotel's overview data.</returns>
    /// <response code="200">The hotel overview was returned.</response>
    /// <response code="404">No hotel has the specified identifier.</response>
    [HttpGet("hotel/{hotelId:long}")]
    [ProducesResponseType(typeof(OverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OverviewResponse>> GetHotelOverview(
        long hotelId,
        [FromQuery] DateOnly date,
        [FromQuery] DateOnly? trendDate,
        CancellationToken cancellationToken)
    {
        var overview = await overviewService.GetHotelOverviewAsync(
            hotelId, date, trendDate, cancellationToken);
        return overview is null ? NotFound() : Ok(overview);
    }
}
