namespace Gigs.DTOs;

public class TopCityResponse
{
    public string City { get; set; } = string.Empty;
    public int GigCount { get; set; }
    public int UniqueVenues { get; set; }
}
