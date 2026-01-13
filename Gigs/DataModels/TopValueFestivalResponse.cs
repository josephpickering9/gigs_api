using System;

namespace Gigs.DataModels;

public class TopValueFestivalResponse
{
    public string FestivalName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? Year { get; set; }
    public decimal Price { get; set; }
    public int ActCount { get; set; }
    public decimal PricePerAct { get; set; }
}
