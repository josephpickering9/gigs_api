using Microsoft.AspNetCore.Mvc;
using Gigs.DTOs;
using Gigs.Services;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("stats/total-gigs")]
    public async Task<ActionResult<DashboardStatsResponse>> GetTotalGigs()
    {
        var stats = await dashboardService.GetDashboardStatsAsync();
        return Ok(stats);
    }
}
