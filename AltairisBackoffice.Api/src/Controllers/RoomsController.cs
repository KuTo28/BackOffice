using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Room;
using AltairisBackoffice.Api.src.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.src.Controllers;

/// <summary>Provides operations for managing hotel rooms.</summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController(
    AltairisDbContext dbContext,
    IHotelInventoryCache inventoryCache) : ControllerBase
{
    private readonly AltairisDbContext _dbContext = dbContext;
    private readonly IHotelInventoryCache _inventoryCache = inventoryCache;

    /// <summary>Gets all rooms.</summary>
    /// <returns>The list of rooms.</returns>
    /// <response code="200">The room list was returned; it may be empty.</response>
    [HttpGet]
    public async Task<ActionResult<List<Room>>> GetAllRooms()
    {
        var rooms = await _dbContext.Rooms.Select(r => r).ToListAsync();
        if (rooms == null)
        {
            return NotFound();
        }
        return Ok(rooms);
    }

    /// <summary>Gets a room by its identifier.</summary>
    /// <param name="id">The room identifier.</param>
    /// <returns>The matching room.</returns>
    /// <response code="200">The room was found.</response>
    /// <response code="404">No room has the specified identifier.</response>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<Room>> GetRoom(long id)
    {
        var room = await _dbContext.Rooms.FindAsync(id);
        if (room == null)
        {
            return NotFound();
        }
        return Ok(room);
    }

    /// <summary>Gets all rooms belonging to a hotel.</summary>
    /// <param name="hotelId">The hotel identifier.</param>
    /// <returns>The rooms associated with the hotel.</returns>
    /// <response code="200">The room list was returned; it may be empty.</response>
    [HttpGet("hotel/{hotelId:long}")]
    public async Task<ActionResult<List<Room>>> GetRoomsByHotel(long hotelId)
    {
        var rooms = await _dbContext.Rooms.Where(r => r.HotelId == hotelId).ToListAsync();
        if (rooms == null)
        {
            return NotFound();
        }
        return Ok(rooms);
    }

    /// <summary>Creates a room.</summary>
    /// <param name="roomCommand">The room values to create.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The newly created room.</returns>
    /// <response code="201">The room was created.</response>
    /// <response code="400">The request body is invalid.</response>
    [HttpPost]
    public async Task<ActionResult<Room>> CreateRoom(
        RoomCreateCommand roomCommand,
        CancellationToken cancellationToken)
    {
        if (roomCommand == null)
        {
            return BadRequest();
        }

        if (!await _dbContext.Hotels.AnyAsync(
            hotel => hotel.Id == roomCommand.HotelId, cancellationToken))
        {
            return BadRequest("The selected hotel does not exist.");
        }

        var room = new Room
        {
            Name = roomCommand.Name,
            Description = roomCommand.Description,
            Price = roomCommand.Price,
            HotelId = roomCommand.HotelId
        };

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _inventoryCache.RefreshHotelAsync(room.HotelId, CancellationToken.None);
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, room);
    }

    /// <summary>Updates an existing room.</summary>
    /// <param name="id">The room identifier.</param>
    /// <param name="updatedRoom">The room values to update.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content when the update succeeds.</returns>
    /// <response code="204">The room was updated.</response>
    /// <response code="404">No room has the specified identifier.</response>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateRoom(
        long id,
        [FromBody] RoomUpdateCommand updatedRoom,
        CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);

        if (room == null)
        {
            return NotFound();
        }

        room.Name = updatedRoom.Name ?? room.Name;
        room.Description = updatedRoom.Description ?? room.Description;
        room.Price = updatedRoom.Price ?? room.Price;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _inventoryCache.RefreshHotelAsync(room.HotelId, CancellationToken.None);

        return NoContent();
    }

    /// <summary>Deletes a room.</summary>
    /// <param name="id">The room identifier.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>No content when the deletion succeeds.</returns>
    /// <response code="204">The room was deleted.</response>
    /// <response code="404">No room has the specified identifier.</response>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteRoom(long id, CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms.FindAsync([id], cancellationToken);
        if (room == null)
        {
            return NotFound();
        }

        var hotelId = room.HotelId;
        _dbContext.Rooms.Remove(room);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _inventoryCache.RefreshHotelAsync(hotelId, CancellationToken.None);

        return NoContent();
    }
}