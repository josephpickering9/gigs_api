namespace Gigs.DataModels;

public class DashboardStatsResponse
{
    public int TotalGigs { get; set; }
    public TopArtistStats? TopArtist { get; set; }
    public TopVenueStats? TopVenue { get; set; }
    public TopCityStats? TopCity { get; set; }
    public TopAttendeeStats? TopAttendee { get; set; }
}

public class TopArtistStats
{
    public string ArtistName { get; set; } = string.Empty;
    public int GigCount { get; set; }
}

public class TopVenueStats
{
    public string VenueName { get; set; } = string.Empty;
    public int GigCount { get; set; }
}

public class TopCityStats
{
    public string CityName { get; set; } = string.Empty;
    public int GigCount { get; set; }
}

public class TopAttendeeStats
{
    public string PersonName { get; set; } = string.Empty;
    public int GigCount { get; set; }
}
