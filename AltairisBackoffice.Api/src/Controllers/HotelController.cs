using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Hotel;
using AltairisBackoffice.Api.src.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.src.Controllers;

/// <summary>Provides operations for managing hotels.</summary>
[ApiController]
[Route("api/[controller]")]
public class HotelController(
    AltairisDbContext dbContext,
    IHotelInventoryCache inventoryCache) : ControllerBase
{
    private readonly AltairisDbContext _dbContext = dbContext;
    private readonly IHotelInventoryCache _inventoryCache = inventoryCache;

    /// <summary>Gets all hotels.</summary>
    /// <returns>The list of hotels.</returns>
    /// <response code="200">The hotel list was returned; it may be empty.</response>
    [HttpGet]
    public async Task<ActionResult<List<Hotel>>> GetAllHotels()
    {
        var hotels = await _dbContext.Hotels.Select(h => h).ToListAsync();
        if (hotels == null)
        {
            return NotFound();
        }
        return Ok(hotels);
    }

    /// <summary>Searches hotels by name, city, country, or email with pagination.</summary>
    /// <param name="query">Optional text to match against hotel details.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of results per page, capped at 100.</param>
    /// <returns>The matching hotels, total result count, and pagination values.</returns>
    /// <response code="200">The matching hotels were returned.</response>
    [HttpGet("search")]
    public async Task<ActionResult<object>> SearchHotels(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var hotelsQuery = _dbContext.Hotels.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim().ToLowerInvariant();
            hotelsQuery = hotelsQuery.Where(h =>
                h.Name.ToLower().Contains(search) ||
                (h.City != null && h.City.ToLower().Contains(search)) ||
                (h.Country != null && h.Country.ToLower().Contains(search)) ||
                h.Email.ToLower().Contains(search));
        }

        var totalCount = await hotelsQuery.CountAsync();
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize)));
        var items = await hotelsQuery
            .OrderBy(h => h.Name)
            .ThenBy(h => h.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { items, totalCount, page, pageSize });
    }

    /// <summary>Gets a hotel by its identifier.</summary>
    /// <param name="id">The hotel identifier.</param>
    /// <returns>The matching hotel.</returns>
    /// <response code="200">The hotel was found.</response>
    /// <response code="404">No hotel has the specified identifier.</response>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<Hotel>> GetHotel(long id)
    {
        var hotel = await _dbContext.Hotels.FindAsync(id);
        if (hotel == null)
        {
            return NotFound();
        }
        return Ok(hotel);
    }

    /// <summary>Creates a hotel.</summary>
    /// <param name="hotelCommand">The hotel values to create.</param>
    /// <returns>The newly created hotel.</returns>
    /// <response code="201">The hotel was created.</response>
    /// <response code="400">The request body is invalid.</response>
    [HttpPost]
    public async Task<ActionResult<Hotel>> CreateHotel(HotelCreateCommand hotelCommand)
    {
        if (hotelCommand == null)
        {
            return BadRequest();
        }

        var hotel = new Hotel
        {
            Name = hotelCommand.Name,
            Email = hotelCommand.Email,
            Phone = hotelCommand.Phone,
            Active = hotelCommand.Active,
            Address = hotelCommand.Address,
            City = hotelCommand.City,
            Country = hotelCommand.Country,
            Description = hotelCommand.Description
        };

        _dbContext.Hotels.Add(hotel);
        await _dbContext.SaveChangesAsync();
        await _inventoryCache.RefreshHotelAsync(hotel.Id, CancellationToken.None);
        return CreatedAtAction(nameof(GetHotel), new { id = hotel.Id }, hotel);
    }

    /// <summary>Updates an existing hotel.</summary>
    /// <param name="id">The hotel identifier.</param>
    /// <param name="updatedHotel">The hotel values to update.</param>
    /// <returns>No content when the update succeeds.</returns>
    /// <response code="204">The hotel was updated.</response>
    /// <response code="404">No hotel has the specified identifier.</response>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateHotel(long id, [FromBody] HotelUpdateCommand updatedHotel)
    {
        var hotel = await _dbContext.Hotels.FindAsync(id);

        if (hotel == null)
        {
            return NotFound();
        }

        hotel.Name = updatedHotel.Name ?? hotel.Name;
        hotel.Email = updatedHotel.Email ?? hotel.Email;
        hotel.Phone = updatedHotel.Phone ?? hotel.Phone;
        hotel.Active = updatedHotel.Active ?? hotel.Active;
        hotel.Address = updatedHotel.Address ?? hotel.Address;
        hotel.City = updatedHotel.City ?? hotel.City;
        hotel.Country = updatedHotel.Country ?? hotel.Country;
        hotel.Description = updatedHotel.Description ?? hotel.Description;

        await _dbContext.SaveChangesAsync();
        await _inventoryCache.RefreshHotelAsync(hotel.Id, CancellationToken.None);

        return NoContent();
    }

    /// <summary>Deletes a hotel.</summary>
    /// <param name="id">The hotel identifier.</param>
    /// <returns>No content when the deletion succeeds.</returns>
    /// <response code="204">The hotel was deleted.</response>
    /// <response code="404">No hotel has the specified identifier.</response>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteHotel(long id)
    {
        var hotel = await _dbContext.Hotels.FindAsync(id);
        if (hotel == null)
        {
            return NotFound();
        }

        _dbContext.Hotels.Remove(hotel);
        await _dbContext.SaveChangesAsync();
        await _inventoryCache.RefreshHotelAsync(id, CancellationToken.None);

        return NoContent();
    }
}