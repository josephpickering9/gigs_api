using Gigs.DataModels;
using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(DashboardService dashboardService): ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsResponse>> GetDashboardStats()
    {
        var result = await dashboardService.GetDashboardStatsAsync();
        return result.ToResponse();
    }

    [HttpGet("average-ticket-price-by-year")]
    public async Task<ActionResult<List<AverageTicketPriceByYearResponse>>> GetAverageTicketPriceByYear()
    {
        var result = await dashboardService.GetAverageTicketPriceByYearAsync();
        return result.ToResponse();
    }

    [HttpGet("average-festival-price-by-year")]
    public async Task<ActionResult<List<AverageFestivalPriceByYearResponse>>> GetAverageFestivalPriceByYear()
    {
        var result = await dashboardService.GetAverageFestivalPriceByYearAsync();
        return result.ToResponse();
    }

    [HttpGet("festivals-per-year")]
    public async Task<ActionResult<List<FestivalsPerYearResponse>>> GetFestivalsPerYear()
    {
        var result = await dashboardService.GetFestivalsPerYearAsync();
        return result.ToResponse();
    }

    [HttpGet("gigs-per-year")]
    public async Task<ActionResult<List<GigsPerYearResponse>>> GetGigsPerYear()
    {
        var result = await dashboardService.GetGigsPerYearAsync();
        return result.ToResponse();
    }

    [HttpGet("gigs-per-month")]
    public async Task<ActionResult<List<GigsPerMonthResponse>>> GetGigsPerMonth()
    {
        var result = await dashboardService.GetGigsPerMonthAsync();
        return result.ToResponse();
    }

    [HttpGet("temporal-stats")]
    public async Task<ActionResult<TemporalStatsResponse>> GetTemporalStats()
    {
        var result = await dashboardService.GetTemporalStatsAsync();
        return result.ToResponse();
    }


    [HttpGet("top-artists")]
    public async Task<ActionResult<List<TopArtistResponse>>> GetTopArtists([FromQuery] int limit = 10)
    {
        var result = await dashboardService.GetTopArtistsAsync(limit);
        return result.ToResponse();
    }

    [HttpGet("venue-insights")]
    public async Task<ActionResult<VenueInsightsResponse>> GetVenueInsights()
    {
        var result = await dashboardService.GetVenueInsightsAsync();
        return result.ToResponse();
    }

    [HttpGet("top-venues")]
    public async Task<ActionResult<List<TopVenueResponse>>> GetTopVenues([FromQuery] int limit = 10)
    {
        var result = await dashboardService.GetTopVenuesAsync(limit);
        return result.ToResponse();
    }

    [HttpGet("top-cities")]
    public async Task<ActionResult<List<TopCityResponse>>> GetTopCities([FromQuery] int limit = 10)
    {
        var result = await dashboardService.GetTopCitiesAsync(limit);
        return result.ToResponse();
    }

    [HttpGet("interesting-insights")]
    public async Task<ActionResult<InterestingInsightsResponse>> GetInterestingInsights()
    {
        var result = await dashboardService.GetInterestingInsightsAsync();
        return result.ToResponse();
    }

    [HttpGet("most-heard-songs")]
    public async Task<ActionResult<List<MostHeardSongResponse>>> GetMostHeardSongs([FromQuery] int limit = 10)
    {
        var result = await dashboardService.GetMostHeardSongsAsync(limit);
        return result.ToResponse();
    }


    [HttpGet("top-attendees")]
    public async Task<ActionResult<List<TopAttendeeResponse>>> GetTopAttendees([FromQuery] int limit = 10)
    {
        var result = await dashboardService.GetTopAttendeesAsync(limit);
        return result.ToResponse();
    }

    [HttpGet("top-value-festivals")]
    public async Task<ActionResult<List<TopValueFestivalResponse>>> GetTopValueFestivals([FromQuery] int limit = 5)
    {
        var result = await dashboardService.GetTopValueFestivalsAsync(limit);
        return result.ToResponse();
    }
}
