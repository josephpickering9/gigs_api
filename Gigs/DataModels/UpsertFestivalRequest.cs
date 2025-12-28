using System.ComponentModel.DataAnnotations;

namespace Gigs.DataModels;

public class UpsertFestivalRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public int? Year { get; set; }

    public string? ImageUrl { get; set; }
}
