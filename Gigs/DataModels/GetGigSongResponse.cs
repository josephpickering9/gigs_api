namespace Gigs.DataModels;

public class GetGigSongResponse
{
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsEncore { get; set; }
}
