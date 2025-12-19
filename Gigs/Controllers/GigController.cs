using Microsoft.AspNetCore.Mvc;
using Gigs.DTOs;
using Gigs.Services;
using Gigs.Types;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")] // api/gigs
public class GigController(IGigService gigService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetGigResponse>>> GetAll()
    {
        var gigs = await gigService.GetAllAsync();
        return Ok(gigs);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetGigResponse>> GetById(GigId id)
    {
        var gig = await gigService.GetByIdAsync(id);
        return Ok(gig);
    }

    [HttpPost]
    public async Task<ActionResult<GetGigResponse>> Create(UpsertGigRequest request)
    {
        var gig = await gigService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = gig.Id }, gig);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GetGigResponse>> Update(GigId id, UpsertGigRequest request)
    {
        var gig = await gigService.UpdateAsync(id, request);
        return Ok(gig);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(GigId id)
    {
        await gigService.DeleteAsync(id);
        return NoContent();
    }
}
