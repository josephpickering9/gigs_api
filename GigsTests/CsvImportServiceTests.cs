using System.Text;
using Gigs.Models;
using Gigs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GigsTests;

public class CsvImportServiceTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public CsvImportServiceTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ImportGigs_DelegatesToGigService_AndUpserts()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var csvService = scope.ServiceProvider.GetRequiredService<CsvImportService>();

        // Ensure clean state
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var csvContent = @"Date,Venue,City,Artist / Headliner,Support Acts,Ticket Cost,Ticket Type,Went With,Genre,Setlist URL
2023-11-20,The O2,London,Queen,Adam Lambert,85.00,Seated,John Doe,Rock,http://setlist.fm/queen
2023-11-20,The O2,London,Queen,Adam Lambert,90.00,VIP,,Rock,http://setlist.fm/queen/vip
";
        // Note: Two records. Same Date, Venue, Headliner. Diff cost/type. 
        // Upsert logic should update the first one with the second one's details.

        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var count = await csvService.ImportGigsAsync(memoryStream);

        // Assert
        Assert.Equal(2, count); // Processed 2 records

        var gigs = await db.Gig
            .Include(g => g.Venue)
            .Include(g => g.Acts).ThenInclude(a => a.Artist)
            .ToListAsync();

        Assert.Single(gigs); // Should only be 1 gig due to upsert

        var gig = gigs.First();
        Assert.Equal("The O2", gig.Venue.Name);
        Assert.Equal(new DateOnly(2023, 11, 20), gig.Date);
        Assert.Contains(gig.Acts, a => a.IsHeadliner && a.Artist.Name == "Queen");
        
        // Check updates from second record
        Assert.Equal(TicketType.VIP, gig.TicketType); // Should be updated to VIP
        Assert.Equal(90.00m, gig.TicketCost); // Should be updated to 90
    }
}
