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

public class VenueControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public VenueControllerTests(CustomWebApplicationFactory<Program> factory)
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

            var venue1 = new Venue { Name = "Venue A", City = "City A", Slug = "venue-a" };
            var venue2 = new Venue { Name = "Venue B", City = "City B", Slug = "venue-b" };
            var venue3 = new Venue { Name = "Venue C", City = "City C", Slug = "venue-c" };

            db.Venue.AddRange(venue1, venue2, venue3);
            await db.SaveChangesAsync();

            var artist = new Artist { Name = "Test Artist", Slug = "test-artist" };
            db.Artist.Add(artist);
            await db.SaveChangesAsync();

            // Venue A: 2 gigs
            var gig1 = new Gig
            {
                VenueId = venue1.Id,
                Date = new DateOnly(2024, 1, 1),
                Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }]
            };
            var gig2 = new Gig
            {
                VenueId = venue1.Id,
                Date = new DateOnly(2024, 1, 2),
                Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }]
            };

            // Venue B: 1 gig
            var gig3 = new Gig
            {
                VenueId = venue2.Id,
                Date = new DateOnly(2024, 1, 3),
                Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }]
            };

            // Venue C: 0 gigs

            db.Gig.AddRange(gig1, gig2, gig3);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetAll_ReturnsVenuesWithCorrectGigCounts()
    {
        await SeedData();

        var response = await _client.GetAsync("/api/venues");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<GetVenueResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        var venueA = result.FirstOrDefault(v => v.Name == "Venue A");
        Assert.NotNull(venueA);
        Assert.Equal(2, venueA.GigCount);

        var venueB = result.FirstOrDefault(v => v.Name == "Venue B");
        Assert.NotNull(venueB);
        Assert.Equal(1, venueB.GigCount);

        var venueC = result.FirstOrDefault(v => v.Name == "Venue C");
        Assert.NotNull(venueC);
        Assert.Equal(0, venueC.GigCount);
    }

    [Fact]
    public async Task GetAll_WithCityFilter_ReturnsOnlyVenuesInThatCity()
    {
        await SeedData();

        var response = await _client.GetAsync("/api/venues?city=City A");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<GetVenueResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Venue A", result[0].Name);
        Assert.Equal(2, result[0].GigCount);
    }

    [Fact]
    public async Task GetAll_WithArtistFilter_ReturnsVenuesWhereArtistPlayed()
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
            
            // Artist 2 only plays at venue 1
            db.Gig.Add(new Gig { VenueId = venue1.Id, Date = new DateOnly(2024, 1, 3), Acts = [new GigArtist { ArtistId = artist2.Id, IsHeadliner = true }] });
            await db.SaveChangesAsync();

            var response = await _client.GetAsync($"/api/venues?artistId={artist2.Id}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<GetVenueResponse>>(_jsonOptions);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Venue 1", result[0].Name);
            Assert.Equal(1, result[0].GigCount); // Only 1 gig with Artist 2
        }
    }
}
