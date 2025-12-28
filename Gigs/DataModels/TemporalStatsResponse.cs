namespace Gigs.DataModels;

public class TemporalStatsResponse
{
    public int? BusiestYear { get; set; }

    public int? BusiestYearGigCount { get; set; }

    public int? DaysSinceLastGig { get; set; }
}
