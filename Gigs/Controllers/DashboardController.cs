using Microsoft.AspNetCore.Mvc;
using Gigs.DTOs;
using Gigs.Services;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(DashboardService dashboardService) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> GetDashboardStats()
    {
        var stats = await dashboardService.GetDashboardStatsAsync();
        return Ok(stats);
    }

    [HttpGet("average-ticket-price-by-year")]
    public async Task<ActionResult<List<AverageTicketPriceByYearResponse>>> GetAverageTicketPriceByYear()
    {
        var data = await dashboardService.GetAverageTicketPriceByYearAsync();
        return Ok(data);
    }

    [HttpGet("gigs-per-year")]
    public async Task<ActionResult<List<GigsPerYearResponse>>> GetGigsPerYear()
    {
        var data = await dashboardService.GetGigsPerYearAsync();
        return Ok(data);
    }

    [HttpGet("gigs-per-month")]
    public async Task<ActionResult<List<GigsPerMonthResponse>>> GetGigsPerMonth()
    {
        var data = await dashboardService.GetGigsPerMonthAsync();
        return Ok(data);
    }

    [HttpGet("temporal-stats")]
    public async Task<ActionResult<TemporalStatsResponse>> GetTemporalStats()
    {
        var data = await dashboardService.GetTemporalStatsAsync();
        return Ok(data);
    }

    [HttpGet("artist-insights")]
    public async Task<ActionResult<ArtistInsightsResponse>> GetArtistInsights()
    {
        var data = await dashboardService.GetArtistInsightsAsync();
        return Ok(data);
    }

    [HttpGet("top-artists")]
    public async Task<ActionResult<List<TopArtistResponse>>> GetTopArtists([FromQuery] int limit = 10)
    {
        var data = await dashboardService.GetTopArtistsAsync(limit);
        return Ok(data);
    }

    [HttpGet("venue-insights")]
    public async Task<ActionResult<VenueInsightsResponse>> GetVenueInsights()
    {
        var data = await dashboardService.GetVenueInsightsAsync();
        return Ok(data);
    }

    [HttpGet("top-venues")]
    public async Task<ActionResult<List<TopVenueResponse>>> GetTopVenues([FromQuery] int limit = 10)
    {
        var data = await dashboardService.GetTopVenuesAsync(limit);
        return Ok(data);
    }

    [HttpGet("top-cities")]
    public async Task<ActionResult<List<TopCityResponse>>> GetTopCities([FromQuery] int limit = 10)
    {
        var data = await dashboardService.GetTopCitiesAsync(limit);
        return Ok(data);
    }

    [HttpGet("interesting-insights")]
    public async Task<ActionResult<InterestingInsightsResponse>> GetInterestingInsights()
    {
        var data = await dashboardService.GetInterestingInsightsAsync();
        return Ok(data);
    }

    [HttpGet("most-heard-songs")]
    public async Task<ActionResult<List<MostHeardSongResponse>>> GetMostHeardSongs([FromQuery] int limit = 10)
    {
        var data = await dashboardService.GetMostHeardSongsAsync(limit);
        return Ok(data);
    }

    [HttpGet("attendee-insights")]
    public async Task<ActionResult<AttendeeInsightsResponse>> GetAttendeeInsights()
    {
        var data = await dashboardService.GetAttendeeInsightsAsync();
        return Ok(data);
    }

    [HttpGet("top-attendees")]
    public async Task<ActionResult<List<TopAttendeeResponse>>> GetTopAttendees([FromQuery] int limit = 10)
    {
        var data = await dashboardService.GetTopAttendeesAsync(limit);
        return Ok(data);
    }
}
