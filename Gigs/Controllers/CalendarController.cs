using Gigs.DataModels;
using Gigs.Services.Calendar;
using Gigs.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarController : ControllerBase
{
    private readonly GoogleCalendarService _calendarService;

    public CalendarController(GoogleCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    /// <summary>
    /// Import calendar events as gigs.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>

    [Authorize]
    [HttpPost("import")]
    public async Task<ActionResult<ImportCalendarEventsResponse>> Import([FromBody] ImportCalendarEventsRequest? request = null)
    {
        var result = await _calendarService.ImportEventsAsGigsAsync(
            request?.StartDate,
            request?.EndDate);

        return result.ToResponse();
    }

    /// <summary>
    /// Get calendar events without importing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [Authorize]
    [HttpGet("events")]
    public async Task<ActionResult<List<GetCalendarEventResponse>>> GetEvents([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _calendarService.GetCalendarEventsAsync(startDate, endDate);
        return result.ToResponse();
    }
}
