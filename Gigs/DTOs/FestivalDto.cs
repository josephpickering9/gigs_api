using Gigs.Types;

namespace Gigs.DTOs;

public class FestivalDto
{
    public FestivalId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public List<GetGigResponse>? Gigs { get; set; }
}
