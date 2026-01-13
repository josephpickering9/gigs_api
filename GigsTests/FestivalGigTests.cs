using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gigs.DataModels;
using Gigs.Models;
using Gigs.Types;
using Gigs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GigsTests;

public class FestivalGigTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public FestivalGigTests(CustomWebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task Create_GigWithFestival_ShouldUsesFestivalVenue_AndSucceed_WhenNoVenueProvided()
    {
        // 1. Seed Festival with Venue
        FestivalId festivalId;
        VenueId venueId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            // Ensure clean start if shared DB (InMemory is shared per factory instance usually)
            
            var venue = new Venue { Name = "Festival Park", City = "London", Slug = "fest-park" };
            db.Venue.Add(venue);
            
            var festival = new Festival 
            { 
                Name = "Download 2025", 
                Slug = "download-2025", 
                Venue = venue, 
                StartDate = new DateOnly(2025, 6, 12), 
                EndDate = new DateOnly(2025, 6, 14) 
            };
            db.Festival.Add(festival);
            await db.SaveChangesAsync();
            festivalId = festival.Id;
            venueId = venue.Id;
        }

        // 2. Create Request with ONLY FestivalId (no Venue)
        var request = new UpsertGigRequest
        {
            FestivalId = festivalId.ToString(),
            Date = new DateOnly(2025, 6, 12),
            TicketType = TicketType.Standing,
            Acts = new List<GigArtistRequest>
            {
                new GigArtistRequest { ArtistId = "Metallica", IsHeadliner = true, Order = 0 }
            }
        };

        // 3. Post
        var response = await _client.PostAsJsonAsync("/api/gigs", request, _jsonOptions);
        
        // 4. Assert
        response.EnsureSuccessStatusCode(); 
        var result = await response.Content.ReadFromJsonAsync<GetGigResponse>(_jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Festival Park", result.VenueName);
        Assert.Equal("Download 2025", result.FestivalName);
        Assert.Equal(venueId, result.VenueId);
    }

    [Fact]
    public async Task Create_GigWithFestival_ShouldClearTicketCost()
    {
        // 1. Seed Festival
        FestivalId festivalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            var venue = new Venue { Name = "Festival Park 2", City = "London", Slug = "fest-park-2" };
            db.Venue.Add(venue);
            
            var festival = new Festival 
            { 
                Name = "Reading 2025", 
                Slug = "reading-2025", 
                Venue = venue 
            };
            db.Festival.Add(festival);
            await db.SaveChangesAsync();
            festivalId = festival.Id;
        }

        // 2. Request with TicketCost
        var request = new UpsertGigRequest
        {
            FestivalId = festivalId.ToString(),
            Date = new DateOnly(2025, 8, 25),
            TicketType = TicketType.Standing,
            TicketCost = 100.00m, // Should be ignored/cleared
            Acts = new List<GigArtistRequest>
            {
                new GigArtistRequest { ArtistId = "Foo Fighters", IsHeadliner = true, Order = 0 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/gigs", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GetGigResponse>(_jsonOptions);
        
        Assert.NotNull(result);
        Assert.Null(result.TicketCost);
    }
}
