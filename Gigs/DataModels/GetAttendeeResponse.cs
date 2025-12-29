using Gigs.Types;

namespace Gigs.DataModels;

public class GetAttendeeResponse
{
    public PersonId Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public int GigCount { get; set; }
}
