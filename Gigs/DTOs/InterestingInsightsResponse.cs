namespace Gigs.DTOs;

public class InterestingInsightsResponse
{
    public LongestSetlistInfo? LongestSetlist { get; set; }

    public int? LongestGigStreak { get; set; } // Consecutive months with gigs

    public decimal AverageGigsPerYear { get; set; }
}

public class LongestSetlistInfo
{
    public string GigId { get; set; } = string.Empty;

    public string ArtistName { get; set; } = string.Empty;

    public string VenueName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int SongCount { get; set; }
}
