using Gigs.DataModels;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FestivalsController(FestivalService service): ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetFestivalResponse>>> GetAll()
    {
        var result = await service.GetAllAsync();
        return result.ToResponse();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetFestivalResponse>> GetById(FestivalId id)
    {
        var result = await service.GetByIdAsync(id);
        return result.ToResponse();
    }

    [HttpPost]
    public async Task<ActionResult<GetFestivalResponse>> Create(UpsertFestivalRequest request)
    {
        var result = await service.CreateAsync(request);
        if (result.IsSuccess && result.Data != null)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
        }

        return result.ToResponse();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GetFestivalResponse>> Update(FestivalId id, UpsertFestivalRequest request)
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

    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetFestivalResponse>> Enrich(FestivalId id)
    {
        var result = await service.EnrichFestivalAsync(id);
        return result.ToResponse();
    }

    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAll()
    {
        var result = await service.EnrichAllFestivalsAsync();
        return result.ToResponse();
    }
}
