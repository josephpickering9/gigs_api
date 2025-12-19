using Microsoft.AspNetCore.Mvc;
using Gigs.Services;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("stats/total-gigs")]
    public async Task<ActionResult<int>> GetTotalGigs()
    {
        var count = await dashboardService.GetTotalGigsCountAsync();
        return Ok(count);
    }
}
