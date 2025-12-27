using Microsoft.AspNetCore.Mvc;
using Gigs.Services;
using Gigs.DTOs;
using Gigs.Types;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class VenueController(VenueService venueService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetVenueResponse>>> GetAll()
    {
        var venues = await venueService.GetAllAsync();
        return Ok(venues);
    }

    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetVenueResponse>> EnrichVenue(VenueId id)
    {
        try
        {
            var venue = await venueService.EnrichVenueAsync(id);
            return Ok(venue);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAllVenues()
    {
        var count = await venueService.EnrichAllVenuesAsync();
        return Ok(count);
    }
}
