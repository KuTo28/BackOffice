using AltairisBackoffice.Api.Database.Data.Context;
using AltairisBackoffice.Api.Database.Models.Entities;
using AltairisBackoffice.Api.src.Infrastructure.Dtos.Allotment;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AltairisBackoffice.Api.src.Controllers;

/// <summary>Provides operations for managing room allotments.</summary>
[ApiController]
[Route("api/[controller]")]
public class AllotmentController(AltairisDbContext dbContext) : ControllerBase
{
    private readonly AltairisDbContext _dbContext = dbContext;

    /// <summary>Gets an allotment by its identifier.</summary>
    /// <param name="id">The allotment identifier.</param>
    /// <returns>The matching allotment.</returns>
    /// <response code="200">The allotment was found.</response>
    /// <response code="404">No allotment has the specified identifier.</response>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<Allotment>> GetAllotment(long id)
    {
        var allotment = await _dbContext.Allotments.FindAsync(id);
        if (allotment == null)
        {
            return NotFound();
        }
        return Ok(allotment);
    }

    /// <summary>Gets the allotment assigned to a room.</summary>
    /// <param name="roomId">The room identifier.</param>
    /// <returns>The room's allotment.</returns>
    /// <response code="200">The room and its allotment were found.</response>
    /// <response code="404">The room or its allotment was not found.</response>
    [HttpGet("room/{roomId:long}")]
    public async Task<ActionResult<Allotment>> GetAllotmentByRoom(long roomId)
    {
        var allotment = await _dbContext.Rooms
            .Where(r => r.Id == roomId)
            .Select(r => r.Allotment)
            .FirstOrDefaultAsync();

        if (allotment == null)
        {
            return NotFound();
        }
        return Ok(allotment);
    }

    /// <summary>Creates an allotment.</summary>
    /// <param name="allotmentCommand">The allotment values to create.</param>
    /// <returns>The newly created allotment.</returns>
    /// <response code="201">The allotment was created.</response>
    /// <response code="400">The request body is invalid.</response>
    [HttpPost]
    public async Task<ActionResult<Allotment>> CreateAllotment(AllotmentCreateCommand allotmentCommand)
    {
        if (allotmentCommand == null)
        {
            return BadRequest();
        }

        var allotment = new Allotment
        {
            Value = allotmentCommand.Value,
            NumAdults = allotmentCommand.NumAdults,
            NumChildren = allotmentCommand.NumChildren,
            NumBabies = allotmentCommand.NumBabies,
            FreeSale = allotmentCommand.FreeSale
        };

        _dbContext.Allotments.Add(allotment);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAllotment), new { id = allotment.Id }, allotment);
    }

    /// <summary>Updates an existing allotment.</summary>
    /// <param name="id">The allotment identifier.</param>
    /// <param name="updatedAllotment">The values to update.</param>
    /// <returns>No content when the update succeeds.</returns>
    /// <response code="204">The allotment was updated.</response>
    /// <response code="404">No allotment has the specified identifier.</response>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAllotment(long id, [FromBody] AllotmentUpdateCommand updatedAllotment)
    {
        var allotment = await _dbContext.Allotments.FindAsync(id);

        if (allotment == null)
        {
            return NotFound();
        }

        allotment.Value = updatedAllotment.Value;
        allotment.NumAdults = updatedAllotment.NumAdults ?? allotment.NumAdults;
        allotment.NumChildren = updatedAllotment.NumChildren ?? allotment.NumChildren;
        allotment.NumBabies = updatedAllotment.NumBabies ?? allotment.NumBabies;
        allotment.FreeSale = updatedAllotment.FreeSale ?? allotment.FreeSale;

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }


    /// <summary>Deletes an allotment.</summary>
    /// <param name="id">The allotment identifier.</param>
    /// <returns>No content when the deletion succeeds.</returns>
    /// <response code="204">The allotment was deleted.</response>
    /// <response code="404">No allotment has the specified identifier.</response>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAllotment(long id)
    {
        var allotment = await _dbContext.Allotments.FindAsync(id);
        if (allotment == null)
        {
            return NotFound();
        }

        _dbContext.Allotments.Remove(allotment);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

}