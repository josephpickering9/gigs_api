using Gigs.Types;

namespace Gigs.DataModels;

public class GetGigAttendeeResponse
{
    public PersonId PersonId { get; set; }

    public string PersonName { get; set; } = string.Empty;
}
