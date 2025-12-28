namespace Gigs.DataModels;

public class GigsPerMonthResponse
{
    public int Month { get; set; } // 1-12
    public string MonthName { get; set; } = string.Empty;
    public int GigCount { get; set; }
}
