using Gigs.DataModels;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class GigController(GigService gigService): ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<GetGigResponse>>> GetAll([FromQuery] GetGigsFilter filter)
    {
        var result = await gigService.GetAllAsync(filter);
        return result.ToResponse();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetGigResponse>> GetById(GigId id)
    {
        var result = await gigService.GetByIdAsync(id);
        return result.ToResponse();
    }

    [HttpPost]
    public async Task<ActionResult<GetGigResponse>> Create(UpsertGigRequest request)
    {
        var result = await gigService.CreateAsync(request);
        if (result.IsSuccess && result.Data != null)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data);
        }

        return result.ToResponse();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GetGigResponse>> Update(GigId id, UpsertGigRequest request)
    {
        var result = await gigService.UpdateAsync(id, request);
        return result.ToResponse();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(GigId id)
    {
        var result = await gigService.DeleteAsync(id);
        return result.ToResponse();
    }

    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetGigResponse>> Enrich(GigId id)
    {
        var result = await gigService.EnrichGigAsync(id);
        return result.ToResponse();
    }

    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAllGigs()
    {
        var result = await gigService.EnrichAllGigsAsync();
        return result.ToResponse();
    }
}
