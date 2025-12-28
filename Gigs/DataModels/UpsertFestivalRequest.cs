using System.ComponentModel.DataAnnotations;

namespace Gigs.DataModels;

public class UpsertFestivalRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public int? Year { get; set; }

    public string? ImageUrl { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal? Price { get; set; }
    public List<string> Attendees { get; set; } = [];
    public List<FestivalGigOrderRequest> Gigs { get; set; } = [];
}

public class FestivalGigOrderRequest
{
    public string GigId { get; set; } = string.Empty;
    public int Order { get; set; }
}
