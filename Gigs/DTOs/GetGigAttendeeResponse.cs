using Gigs.Types;

namespace Gigs.DTOs;

public class GetGigAttendeeResponse
{
    public PersonId PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
}
