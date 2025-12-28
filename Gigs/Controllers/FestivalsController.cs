using Gigs.DTOs;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FestivalsController(FestivalService service): ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FestivalDto>>> GetAll()
    {
        var result = await service.GetAllAsync();
        return result.ToResponse();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FestivalDto>> GetById(FestivalId id)
    {
        var result = await service.GetByIdAsync(id);
        return result.ToResponse();
    }

    [HttpPost]
    public async Task<ActionResult<FestivalDto>> Create(UpsertFestivalRequest request)
    {
        var result = await service.CreateAsync(request);
        if (result.IsSuccess && result.Data != null)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
        }

        return result.ToResponse();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FestivalDto>> Update(FestivalId id, UpsertFestivalRequest request)
    {
        var result = await service.UpdateAsync(id, request);
        return result.ToResponse();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(FestivalId id)
    {
        var result = await service.DeleteAsync(id);
        return result.ToResponse();
    }
}
