using Gigs.DTOs;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FestivalsController(IFestivalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FestivalDto>>> GetAll()
    {
        return Ok(await service.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FestivalDto>> GetById(FestivalId id)
    {
        var festival = await service.GetByIdAsync(id);
        if (festival == null) return NotFound();
        return Ok(festival);
    }

    [HttpPost]
    public async Task<ActionResult<FestivalDto>> Create(UpsertFestivalRequest request)
    {
        var festival = await service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = festival.Id }, festival);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FestivalDto>> Update(FestivalId id, UpsertFestivalRequest request)
    {
        return Ok(await service.UpdateAsync(id, request));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(FestivalId id)
    {
        await service.DeleteAsync(id);
        return NoContent();
    }
}
