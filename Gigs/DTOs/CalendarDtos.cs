namespace Gigs.DTOs;

public class CalendarEventDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
}

public class ImportCalendarEventsRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ImportCalendarEventsResponse
{
    public int EventsFound { get; set; }
    public int GigsCreated { get; set; }
    public int GigsUpdated { get; set; }
    public int EventsSkipped { get; set; }
    public int VenuesCreated { get; set; }
    public string Message { get; set; } = null!;
}

public class CalendarAuthorizationResponse
{
    public string AuthorizationUrl { get; set; } = null!;
}

public class CalendarStatusResponse
{
    public bool IsConnected { get; set; }
    public string? UserEmail { get; set; }
}
