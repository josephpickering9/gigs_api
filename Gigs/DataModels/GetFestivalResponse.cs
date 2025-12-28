using Gigs.Types;

namespace Gigs.DataModels;

public class GetFestivalResponse
{
    public FestivalId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public List<GetGigResponse>? Gigs { get; set; }
    
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? Price { get; set; }
    public decimal? DailyPrice { get; set; }
    
    public List<GetPersonResponse> Attendees { get; set; } = [];
}
