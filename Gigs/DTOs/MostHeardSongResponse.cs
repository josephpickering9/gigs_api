namespace Gigs.DTOs;

public class MostHeardSongResponse
{
    public string SongId { get; set; } = string.Empty;

    public string SongTitle { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public int TimesHeard { get; set; }
}
