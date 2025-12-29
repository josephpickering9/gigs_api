using Gigs.Types;

namespace Gigs.DataModels;

public class GetArtistResponse
{
    public ArtistId Id { get; set; } = ArtistId.New();
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int GigCount { get; set; }
}
