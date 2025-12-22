using System.ComponentModel.DataAnnotations;

namespace Gigs.DTOs;

public class UpsertFestivalRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public int? Year { get; set; }
    
    public string? ImageUrl { get; set; }
}
