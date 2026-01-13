using Gigs.DataModels;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class VenueController(VenueService venueService): ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetVenueResponse>>> GetAll([FromQuery] GigFilterCriteria? filter)
    {
        var result = await venueService.GetAllAsync(filter);
        return result.ToResponse();
    }

    [Authorize]
    [HttpPost("{id}/enrich")]
    public async Task<ActionResult<GetVenueResponse>> EnrichVenue(VenueId id)
    {
        var result = await venueService.EnrichVenueAsync(id);
        return result.ToResponse();
    }

    [Authorize]
    [HttpPost("enrich-all")]
    public async Task<ActionResult<int>> EnrichAllVenues()
    {
        var result = await venueService.EnrichAllVenuesAsync();
        return result.ToResponse();
    }
}
