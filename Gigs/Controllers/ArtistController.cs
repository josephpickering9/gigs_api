using Microsoft.AspNetCore.Mvc;
using Gigs.Services;
using Gigs.DTOs;
using Gigs.Types;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class ArtistController(ArtistService artistService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetArtistResponse>>> GetAll()
    {
        var artists = await artistService.GetAllAsync();
        return Ok(artists);
    }

    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetArtistResponse>> EnrichArtist(ArtistId id)
    {
        try
        {
            var artist = await artistService.EnrichArtistAsync(id);
            return Ok(artist);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAllArtists()
    {
        var count = await artistService.EnrichAllArtistsAsync();
        return Ok(count);
    }
}
