using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Festival
{
    [Required] public FestivalId Id { get; set; } = FestivalId.New();
    
    [Required] public string Name { get; set; } = string.Empty;
    
    [Required] public string Slug { get; set; } = Guid.NewGuid().ToString();
    
    public int? Year { get; set; }
    
    public string? ImageUrl { get; set; }
    
    public List<Gig> Gigs { get; set; } = [];
}
