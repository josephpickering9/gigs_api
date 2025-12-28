using System.ComponentModel.DataAnnotations;

namespace Gigs.DataModels;

public class UpsertPersonRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
