using Gigs.Types;

namespace Gigs.DataModels;

public class GetGigArtistResponse
{
    public ArtistId ArtistId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsHeadliner { get; set; }
    public string? ImageUrl { get; set; }
    public List<GetGigSongResponse> Setlist { get; set; } = [];
}
