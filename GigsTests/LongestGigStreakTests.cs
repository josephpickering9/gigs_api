using Gigs.Services;
using Gigs.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GigsTests;

public class LongestGigStreakTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public LongestGigStreakTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LongestGigStreak_ConsecutiveMonths_ReturnsCorrectStreak()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
        var artist = new Artist { Name = "Test Artist", Slug = "test-artist" };
        db.Venue.Add(venue);
        db.Artist.Add(artist);
        await db.SaveChangesAsync();

        // Create gigs for 5 consecutive months: Jan-May 2023
        var gigs = new List<Gig>
        {
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 1, 15), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 2, 10), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 3, 5), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 4, 20), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 5, 12), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
        };

        db.Gig.AddRange(gigs);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetInterestingInsightsAsync();

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.LongestGigStreak);
    }

    [Fact]
    public async Task LongestGigStreak_WithGap_ReturnsLongestConsecutiveStreak()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
        var artist = new Artist { Name = "Test Artist", Slug = "test-artist" };
        db.Venue.Add(venue);
        db.Artist.Add(artist);
        await db.SaveChangesAsync();

        // Create gigs: Jan-Mar 2023, then skip April, then May-Aug 2023
        // Longest streak should be 4 (May-Aug)
        var gigs = new List<Gig>
        {
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 1, 15), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 2, 10), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 3, 5), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            // April is skipped
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 5, 20), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 6, 12), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 7, 8), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 8, 25), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
        };

        db.Gig.AddRange(gigs);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetInterestingInsightsAsync();

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Data.LongestGigStreak); // May-Aug is 4 months
    }

    [Fact]
    public async Task LongestGigStreak_MultipleGigsInSameMonth_CountsAsOne()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
        var artist = new Artist { Name = "Test Artist", Slug = "test-artist" };
        db.Venue.Add(venue);
        db.Artist.Add(artist);
        await db.SaveChangesAsync();

        // Create multiple gigs in Jan, Feb, Mar 2023
        var gigs = new List<Gig>
        {
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 1, 5), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 1, 15), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 1, 25), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 2, 10), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 2, 20), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
            new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 3, 5), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] },
        };

        db.Gig.AddRange(gigs);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetInterestingInsightsAsync();

        // Assert - Should be 3 months (Jan, Feb, Mar), not 6 gigs
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.LongestGigStreak);
    }

    [Fact]
    public async Task LongestGigStreak_NoGigs_ReturnsNull()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        // Act
        var result = await service.GetInterestingInsightsAsync();

        // Assert
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.LongestGigStreak);
    }

    [Fact]
    public async Task LongestGigStreak_SingleMonth_ReturnsOne()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
        var artist = new Artist { Name = "Test Artist", Slug = "test-artist" };
        db.Venue.Add(venue);
        db.Artist.Add(artist);
        await db.SaveChangesAsync();

        var gig = new Gig { VenueId = venue.Id, Date = new DateOnly(2023, 1, 15), Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }] };
        db.Gig.Add(gig);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetInterestingInsightsAsync();

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.LongestGigStreak);
    }
}
