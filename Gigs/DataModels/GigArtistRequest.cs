using Gigs.Types;

namespace Gigs.DataModels;

public class GigArtistRequest
{
    public string ArtistId { get; set; }

    public bool IsHeadliner { get; set; }

    public int Order { get; set; }

    public string? SetlistUrl { get; set; }

    public List<string> Setlist { get; set; } =[];
}
