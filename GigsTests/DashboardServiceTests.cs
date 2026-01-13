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

    [Fact]
    public async Task GetTopValueFestivalsAsync_ReturnsCorrectValues()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<DashboardService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        // Artists
        var a1 = new Artist { Name = "A1", Slug = "a1" };
        var a2 = new Artist { Name = "A2", Slug = "a2" };
        var a3 = new Artist { Name = "A3", Slug = "a3" };
        db.Artist.AddRange(a1, a2, a3);
        await db.SaveChangesAsync();

        // Venue
        var venue = new Venue { Name = "V1", Slug = "v1", City = "London" };
        db.Venue.Add(venue);
        await db.SaveChangesAsync();

        // Festival 1: Price 100, 2 Acts (A1, A2) -> 50/act (Rank 3)
        var f1 = new Festival { Name = "F1", Slug = "f1", Price = 100 };
        var g1 = new Gig { Date = DateOnly.FromDateTime(DateTime.Now), VenueId = venue.Id, Slug = "g1" };
        g1.Acts.Add(new GigArtist { ArtistId = a1.Id });
        g1.Acts.Add(new GigArtist { ArtistId = a2.Id });
        f1.Gigs.Add(g1);

        // Festival 2: Price 80, 4 Acts (A1, A2, A3, + A1 Duplicate) -> 3 Unique Acts -> 26.66/act (Rank 1)
        var f2 = new Festival { Name = "F2", Slug = "f2", Price = 80 };
        var g2 = new Gig { Date = DateOnly.FromDateTime(DateTime.Now), VenueId = venue.Id, Slug = "g2" };
        g2.Acts.Add(new GigArtist { ArtistId = a1.Id });
        g2.Acts.Add(new GigArtist { ArtistId = a2.Id });
        g2.Acts.Add(new GigArtist { ArtistId = a3.Id });
        var g2b = new Gig { Date = DateOnly.FromDateTime(DateTime.Now), VenueId = venue.Id, Slug = "g2b" };
        g2b.Acts.Add(new GigArtist { ArtistId = a1.Id }); // Duplicate artist
        f2.Gigs.Add(g2);
        f2.Gigs.Add(g2b);

        // Festival 3: Price 90, 2 Acts (A1, A1) -> 1 Unique Act -> 90/act (Rank 4)
        var f3 = new Festival { Name = "F3", Slug = "f3", Price = 90 };
        var g3 = new Gig { Date = DateOnly.FromDateTime(DateTime.Now), VenueId = venue.Id, Slug = "g3" };
        g3.Acts.Add(new GigArtist { ArtistId = a1.Id });
        var g3b = new Gig { Date = DateOnly.FromDateTime(DateTime.Now), VenueId = venue.Id, Slug = "g3b" };
        g3b.Acts.Add(new GigArtist { ArtistId = a1.Id });
        f3.Gigs.Add(g3);
        f3.Gigs.Add(g3b);

        // Festival 4: Price 0 (Ignored)
        var f4 = new Festival { Name = "F4", Slug = "f4", Price = 0 };
        f4.Gigs.Add(new Gig { Date = DateOnly.FromDateTime(DateTime.Now), VenueId = venue.Id, Slug = "g4", Acts = { new GigArtist { ArtistId = a1.Id } } });

        // Festival 5: Price 100, No Acts (Ignored)
        var f5 = new Festival { Name = "F5", Slug = "f5", Price = 100 };

        db.Festival.AddRange(f1, f2, f3, f4, f5);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetTopValueFestivalsAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count); // F4 and F5 ignored

        // Rank 1: F2 (80 / 3 = 26.67)
        Assert.Equal("F2", result[0].FestivalName);
        Assert.Equal(3, result[0].ActCount);
        Assert.Equal(26.67m, result[0].PricePerAct);

        // Rank 2: F1 (100 / 2 = 50.00)
        Assert.Equal("F1", result[1].FestivalName);
        Assert.Equal(2, result[1].ActCount);
        Assert.Equal(50.00m, result[1].PricePerAct);

        // Rank 3: F3 (90 / 1 = 90.00)
        Assert.Equal("F3", result[2].FestivalName);
        Assert.Equal(1, result[2].ActCount);
        Assert.Equal(90.00m, result[2].PricePerAct);
    }
}
