namespace Gigs.DataModels;

public class TopArtistResponse
{
    public string ArtistId { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public int TotalAppearances { get; set; }
    public int AsHeadliner { get; set; }
    public int AsSupport { get; set; }
}
