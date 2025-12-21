using System.ComponentModel.DataAnnotations;
using Gigs.Models;
using Gigs.Types;

namespace Gigs.DTOs;

public class UpsertGigRequest
{
    public VenueId? VenueId { get; set; }
    
    public string? VenueName { get; set; }
    
    public string? VenueCity { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public decimal? TicketCost { get; set; }

    [Required]
    public TicketType TicketType { get; set; }

    public string? ImageUrl { get; set; }
    
    public List<GigArtistRequest> Acts { get; set; } = [];
}
