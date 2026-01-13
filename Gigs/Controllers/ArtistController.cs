using Gigs.DataModels;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class ArtistController(ArtistService artistService): ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetArtistResponse>>> GetAll([FromQuery] GigFilterCriteria? filter)
    {
        var result = await artistService.GetAllAsync(filter);
        return result.ToResponse();
    }

    [Authorize]
    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetArtistResponse>> EnrichArtist(ArtistId id)
    {
        var result = await artistService.EnrichArtistAsync(id);
        return result.ToResponse();
    }

    [Authorize]
    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAllArtists()
    {
        var result = await artistService.EnrichAllArtistsAsync();
        return result.ToResponse();
    }
}
