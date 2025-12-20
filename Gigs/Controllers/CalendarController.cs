using Gigs.DTOs;
using Gigs.Services.Calendar;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly IGoogleCalendarService _calendarService;

    public CalendarController(IGoogleCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    /// <summary>
    /// Import calendar events as gigs
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportCalendarEventsRequest? request = null)
    {
        try
        {
            var result = await _calendarService.ImportEventsAsGigsAsync(
                request?.StartDate,
                request?.EndDate
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = $"Error importing calendar events: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get calendar events without importing
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var events = await _calendarService.GetCalendarEventsAsync(startDate, endDate);
            return Ok(events);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = $"Error fetching calendar events: {ex.Message}" });
        }
    }
}
