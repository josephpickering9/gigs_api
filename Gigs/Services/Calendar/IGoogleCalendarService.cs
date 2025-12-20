using Gigs.DTOs;

namespace Gigs.Services.Calendar;

public interface IGoogleCalendarService
{
    /// <summary>
    /// Get calendar events for a specific date range
    /// </summary>
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(DateTime? startDate = null, DateTime? endDate = null);
    
    /// <summary>
    /// Import calendar events as gigs
    /// </summary>
    Task<ImportCalendarEventsResponse> ImportEventsAsGigsAsync(DateTime? startDate = null, DateTime? endDate = null);
}
