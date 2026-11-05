using AltairisBackoffice.Api.src.Infrastructure.Dtos.Inventory;
using AltairisBackoffice.Api.src.Services;
using Microsoft.AspNetCore.Mvc;

namespace AltairisBackoffice.Api.src.Controllers;

/// <summary>Provides inventory calendar reads and date-range inventory updates.</summary>
[ApiController]
[Route("api/[controller]")]
public class InventoryController(IInventoryCalendarService inventoryCalendarService) : ControllerBase
{
    private readonly IInventoryCalendarService _inventoryCalendarService = inventoryCalendarService;

    /// <summary>Gets the aggregated daily inventory calendar for a hotel.</summary>
    /// <param name="hotelId">The hotel identifier.</param>
    /// <param name="from">The first calendar day.</param>
    /// <param name="to">The last calendar day.</param>
    /// <param name="roomId">Optional room type identifier.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>One aggregated inventory result per day.</returns>
    /// <response code="200">The daily inventory calendar was returned.</response>
    /// <response code="400">The date range is invalid.</response>
    /// <response code="404">The hotel or room type does not exist.</response>
    [HttpGet("calendar")]
    [ProducesResponseType(typeof(IReadOnlyList<InventoryCalendarDayResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<InventoryCalendarDayResponse>>> GetCalendar(
        [FromQuery] long hotelId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] long? roomId,
        CancellationToken cancellationToken)
    {
        if (!IsValidRange(from, to))
        {
            return BadRequest("The date range must be between 1 and 366 days.");
        }

        var days = await _inventoryCalendarService.GetCalendarAsync(
            hotelId, roomId, from, to, cancellationToken);
        return days is null ? NotFound() : Ok(days);
    }

    /// <summary>Gets room-type inventory and booked counts for each requested day.</summary>
    /// <param name="hotelId">The hotel identifier.</param>
    /// <param name="from">The first date in the inclusive range.</param>
    /// <param name="to">The last date in the inclusive range.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>Configured inventory for the hotel's room types and requested dates.</returns>
    /// <response code="200">The inventory was returned.</response>
    /// <response code="400">The date range is invalid or exceeds 366 days.</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryDayResponse>>> GetInventory(
        [FromQuery] long hotelId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (!IsValidRange(from, to))
        {
            return BadRequest("The date range must be between 1 and 366 days.");
        }

        return Ok(await _inventoryCalendarService.GetInventoryAsync(
            hotelId, from, to, cancellationToken));
    }

    /// <summary>Updates capacity and free-sale settings for one room type over an inclusive date range.</summary>
    /// <param name="roomId">The room type identifier.</param>
    /// <param name="command">The date range and inventory values to apply.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content when all dates are updated.</returns>
    /// <response code="204">The inventory was updated.</response>
    /// <response code="400">The date range or capacity is invalid.</response>
    /// <response code="404">The room type does not exist.</response>
    /// <response code="409">The capacity is below existing bookings or a concurrent update occurred.</response>
    [HttpPut("room/{roomId:long}/range")]
    public async Task<IActionResult> UpdateInventory(
        long roomId,
        [FromBody] InventoryUpdateCommand command,
        CancellationToken cancellationToken)
    {
        if (!IsValidRange(command.From, command.To) || command.Capacity < 0)
        {
            return BadRequest("Use a date range of 1 to 366 days and a non-negative capacity.");
        }

        var result = await _inventoryCalendarService.UpdateInventoryAsync(
            roomId, command, cancellationToken);
        return result.Status switch
        {
            InventoryUpdateStatus.Updated => NoContent(),
            InventoryUpdateStatus.RoomNotFound => NotFound(),
            InventoryUpdateStatus.CapacityConflict => Conflict(
                $"Capacity for {result.ConflictDate:yyyy-MM-dd} cannot be lower than {result.Booked} existing bookings."),
            InventoryUpdateStatus.ConcurrentUpdate => Conflict(
                "Inventory changed during this update. Reload the dates and try again."),
            _ => throw new InvalidOperationException($"Unexpected inventory update status: {result.Status}.")
        };
    }

    /// <summary>Applies the same capacity and free-sale settings to multiple room types.</summary>
    /// <param name="command">The room types, inclusive date range, and inventory values.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content when all room types and dates are updated.</returns>
    /// <response code="204">The inventory was updated.</response>
    /// <response code="400">The room selection, date range, or capacity is invalid.</response>
    /// <response code="404">At least one room type does not exist.</response>
    /// <response code="409">The capacity is below existing bookings or a concurrent update occurred.</response>
    [HttpPut("rooms/range")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateInventoryForRooms(
        [FromBody] InventoryBatchUpdateCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RoomIds.Count == 0 ||
            command.RoomIds.Any(roomId => roomId <= 0) ||
            !IsValidRange(command.From, command.To) ||
            command.Capacity < 0)
        {
            return BadRequest("Select at least one valid room type and use a date range of 1 to 366 days with a non-negative capacity.");
        }

        var result = await _inventoryCalendarService.UpdateInventoryAsync(
            command.RoomIds, new InventoryUpdateCommand
            {
                From = command.From,
                To = command.To,
                Capacity = command.Capacity,
                FreeSale = command.FreeSale
            }, cancellationToken);
        return result.Status switch
        {
            InventoryUpdateStatus.Updated => NoContent(),
            InventoryUpdateStatus.RoomNotFound => NotFound(),
            InventoryUpdateStatus.CapacityConflict => Conflict(
                $"Capacity for {result.ConflictDate:yyyy-MM-dd} cannot be lower than {result.Booked} existing bookings."),
            InventoryUpdateStatus.ConcurrentUpdate => Conflict(
                "Inventory changed during this update. Reload the dates and try again."),
            _ => throw new InvalidOperationException($"Unexpected inventory update status: {result.Status}.")
        };
    }

    private static bool IsValidRange(DateOnly from, DateOnly to) =>
        from <= to && to < DateOnly.MaxValue && to.DayNumber - from.DayNumber < 366;

}
