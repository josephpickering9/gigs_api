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
        var result = await venueService.GetAllAsync();
        return result.ToResponse();
    }

    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetVenueResponse>> EnrichVenue(VenueId id)
    {
        var result = await venueService.EnrichVenueAsync(id);
        return result.ToResponse();
    }

    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAllVenues()
    {
        var result = await venueService.EnrichAllVenuesAsync();
        return result.ToResponse();
    }
}
