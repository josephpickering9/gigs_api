using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gigs.DataModels;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GigsTests;

public class ArtistControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ArtistControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        _jsonOptions.Converters.Add(new IdJsonConverterFactory());
    }

    private async Task SeedData()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            var artist1 = new Artist { Name = "Artist A", Slug = "artist-a" };
            var artist2 = new Artist { Name = "Artist B", Slug = "artist-b" };
            var artist3 = new Artist { Name = "Artist C", Slug = "artist-c" };

            db.Artist.AddRange(artist1, artist2, artist3);
            await db.SaveChangesAsync();

            var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
            db.Venue.Add(venue);
            await db.SaveChangesAsync();

            // Artist A: 3 gigs (2 as headliner, 1 as support)
            var gig1 = new Gig
            {
                VenueId = venue.Id,
                Date = new DateOnly(2024, 1, 1),
                Acts = [new GigArtist { ArtistId = artist1.Id, IsHeadliner = true }]
            };
            var gig2 = new Gig
            {
                VenueId = venue.Id,
                Date = new DateOnly(2024, 1, 2),
                Acts = [new GigArtist { ArtistId = artist1.Id, IsHeadliner = true }]
            };
            var gig3 = new Gig
            {
                VenueId = venue.Id,
                Date = new DateOnly(2024, 1, 3),
                Acts = [new GigArtist { ArtistId = artist1.Id, IsHeadliner = false }]
            };

            // Artist B: 1 gig
            var gig4 = new Gig
            {
                VenueId = venue.Id,
                Date = new DateOnly(2024, 1, 4),
                Acts = [new GigArtist { ArtistId = artist2.Id, IsHeadliner = true }]
            };

            // Artist C: 0 gigs

            db.Gig.AddRange(gig1, gig2, gig3, gig4);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetAll_ReturnsArtistsWithCorrectGigCounts()
    {
        await SeedData();

        var response = await _client.GetAsync("/api/artists");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<GetArtistResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        var artistA = result.FirstOrDefault(a => a.Name == "Artist A");
        Assert.NotNull(artistA);
        Assert.Equal(3, artistA.GigCount);

        var artistB = result.FirstOrDefault(a => a.Name == "Artist B");
        Assert.NotNull(artistB);
        Assert.Equal(1, artistB.GigCount);

        var artistC = result.FirstOrDefault(a => a.Name == "Artist C");
        Assert.NotNull(artistC);
        Assert.Equal(0, artistC.GigCount);
    }

    [Fact]
    public async Task GetAll_WithVenueFilter_ReturnsOnlyArtistsWhoPlayedAtThatVenue()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            var venue1 = new Venue { Name = "Venue 1", City = "City 1", Slug = "venue-1" };
            var venue2 = new Venue { Name = "Venue 2", City = "City 2", Slug = "venue-2" };
            db.Venue.AddRange(venue1, venue2);

            var artist1 = new Artist { Name = "Artist 1", Slug = "artist-1" };
            var artist2 = new Artist { Name = "Artist 2", Slug = "artist-2" };
            db.Artist.AddRange(artist1, artist2);
            await db.SaveChangesAsync();

            // Artist 1 plays at both venues
            db.Gig.Add(new Gig { VenueId = venue1.Id, Date = new DateOnly(2024, 1, 1), Acts = [new GigArtist { ArtistId = artist1.Id, IsHeadliner = true }] });
            db.Gig.Add(new Gig { VenueId = venue2.Id, Date = new DateOnly(2024, 1, 2), Acts = [new GigArtist { ArtistId = artist1.Id, IsHeadliner = true }] });
            
            // Artist 2 only plays at venue 2
            db.Gig.Add(new Gig { VenueId = venue2.Id, Date = new DateOnly(2024, 1, 3), Acts = [new GigArtist { ArtistId = artist2.Id, IsHeadliner = true }] });
            await db.SaveChangesAsync();

            var response = await _client.GetAsync($"/api/artists?venueId={venue2.Id}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<GetArtistResponse>>(_jsonOptions);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            var artist1Result = result.FirstOrDefault(a => a.Name == "Artist 1");
            Assert.NotNull(artist1Result);
            Assert.Equal(1, artist1Result.GigCount); // Only 1 gig at venue 2

            var artist2Result = result.FirstOrDefault(a => a.Name == "Artist 2");
            Assert.NotNull(artist2Result);
            Assert.Equal(1, artist2Result.GigCount);
        }
    }

    [Fact]
    public async Task GetAll_WithCityFilter_ReturnsOnlyArtistsWhoPlayedInThatCity()
    {
        await SeedData();

        var response = await _client.GetAsync("/api/artists?city=Test City");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<GetArtistResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Both Artist A and B played in Test City
        
        var artistA = result.FirstOrDefault(a => a.Name == "Artist A");
        Assert.NotNull(artistA);
        Assert.Equal(3, artistA.GigCount);
        
        var artistB = result.FirstOrDefault(a => a.Name == "Artist B");
        Assert.NotNull(artistB);
        Assert.Equal(1, artistB.GigCount);
    }
}
