using Gigs.Types;

namespace Gigs.DTOs;

public class TopAttendeeResponse
{
    public string PersonId { get; set; } = string.Empty;
    public string PersonName { get; set; } = string.Empty;
    public int GigCount { get; set; }
}
