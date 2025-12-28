using Gigs.Services;
using Gigs.Models;
using Gigs.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GigsTests;

public class DashboardServiceTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public DashboardServiceTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAverageFestivalPriceByYearAsync_ReturnsCorrectAverages()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        // Festival 1: 2023, 2 days, 200 price => 100/day
        var f1 = new Festival { Name = "F1", Slug = "f1", StartDate = new DateOnly(2023, 6, 1), EndDate = new DateOnly(2023, 6, 2), Price = 200 };
        
        // Festival 2: 2023, 4 days, 400 price => 100/day
        // Average for 2023 should be 100.
        var f2 = new Festival { Name = "F2", Slug = "f2", StartDate = new DateOnly(2023, 8, 1), EndDate = new DateOnly(2023, 8, 4), Price = 400 };

        // Festival 3: 2024, 1 day, 100 price => 100/day
        var f3 = new Festival { Name = "F3", Slug = "f3", StartDate = new DateOnly(2024, 6, 1), EndDate = new DateOnly(2024, 6, 1), Price = 100 };

        // Festival 4: 2024, 2 days, 300 price => 150/day
        // Average for 2024 should be (100 + 150) / 2 = 125.
        var f4 = new Festival { Name = "F4", Slug = "f4", StartDate = new DateOnly(2024, 7, 1), EndDate = new DateOnly(2024, 7, 2), Price = 300 };

        // Bad Data - Nulls
        var fNullDates = new Festival { Name = "Null Dates", Slug = "null-dates", Price = 100 };
        var fNullPrice = new Festival { Name = "Null Price", Slug = "null-price", StartDate = new DateOnly(2025, 1, 1), EndDate = new DateOnly(2025, 1, 2) };
        
        // Bad Data - End before Start
        var fInvalidDates = new Festival { Name = "Backwards", Slug = "backwards", StartDate = new DateOnly(2025, 1, 5), EndDate = new DateOnly(2025, 1, 4), Price = 100 };

        db.Festival.AddRange(f1, f2, f3, f4, fNullDates, fNullPrice, fInvalidDates);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetAverageFestivalPriceByYearAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        
        var stats2023 = result.FirstOrDefault(x => x.Year == 2023);
        Assert.NotNull(stats2023);
        Assert.Equal(100, stats2023.AverageDailyPrice);

        var stats2024 = result.FirstOrDefault(x => x.Year == 2024);
        Assert.NotNull(stats2024);
        Assert.Equal(125, stats2024.AverageDailyPrice);
    }

    [Fact]
    public async Task GetFestivalsPerYearAsync_ReturnsCorrectCounts()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var f1 = new Festival { Name = "F1", Slug = "f1", StartDate = new DateOnly(2023, 6, 1), EndDate = new DateOnly(2023, 6, 2) };
        var f2 = new Festival { Name = "F2", Slug = "f2", StartDate = new DateOnly(2023, 8, 1), EndDate = new DateOnly(2023, 8, 4) };
        var f3 = new Festival { Name = "F3", Slug = "f3", StartDate = new DateOnly(2024, 6, 1), EndDate = new DateOnly(2024, 6, 1) };
        var fNoDate = new Festival { Name = "F No Date", Slug = "f-no-date" };

        db.Festival.AddRange(f1, f2, f3, fNoDate);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetFestivalsPerYearAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var stats2023 = result.FirstOrDefault(x => x.Year == 2023);
        Assert.NotNull(stats2023);
        Assert.Equal(2, stats2023.FestivalCount);

        var stats2024 = result.FirstOrDefault(x => x.Year == 2024);
        Assert.NotNull(stats2024);
        Assert.Equal(1, stats2024.FestivalCount);
    }
    
    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsTotalFestivals()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var f1 = new Festival { Name = "F1", Slug = "f1", StartDate = new DateOnly(2023, 6, 1) };
        var f2 = new Festival { Name = "F2", Slug = "f2", StartDate = new DateOnly(2023, 8, 1) };
        
        db.Festival.AddRange(f1, f2);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetDashboardStatsAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalFestivals);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsTopFestival()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var f1 = new Festival { Name = "Glastonbury", Slug = "glasto-23", StartDate = new DateOnly(2023, 6, 1) };
        var f2 = new Festival { Name = "Glastonbury", Slug = "glasto-24", StartDate = new DateOnly(2024, 6, 1) };
        var f3 = new Festival { Name = "Download", Slug = "download-23", StartDate = new DateOnly(2023, 8, 1) };
        
        db.Festival.AddRange(f1, f2, f3);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetDashboardStatsAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TopFestival);
        Assert.Equal("Glastonbury", result.TopFestival.FestivalName);
        Assert.Equal(2, result.TopFestival.FestivalCount);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsNextGig()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var venue = new Venue { Name = "V1", City = "C1", Slug = "v1" };
        var artist = new Artist { Name = "A1", Slug = "a1" };
        db.Venue.Add(venue);
        db.Artist.Add(artist);
        await db.SaveChangesAsync();

        var pastGig = new Gig 
        { 
            VenueId = venue.Id, 
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-10)), 
            Slug = "past-gig" 
        };
        var futureGig = new Gig 
        { 
            VenueId = venue.Id, 
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(10)), 
            Slug = "future-gig",
            Acts = new List<GigArtist> 
            { 
                new GigArtist { ArtistId = artist.Id, IsHeadliner = true } 
            }
        };

        db.Gig.AddRange(pastGig, futureGig);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetDashboardStatsAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.NextGig);
        Assert.Equal("V1", result.NextGig.VenueName);
        Assert.Equal("A1", result.NextGig.HeadlineArtist);
        Assert.Equal(futureGig.Date, result.NextGig.Date);
    }
}
