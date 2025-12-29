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

public class AttendeeControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public AttendeeControllerTests(CustomWebApplicationFactory<Program> factory)
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

            // Create Venue
            var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
            db.Venue.Add(venue);

            // Create Artist
            var artist = new Artist { Name = "Test Artist", Slug = "test-artist" };
            db.Artist.Add(artist);

            // Create Persons
            var person1 = new Person { Name = "Alice", Slug = "alice" };
            var person2 = new Person { Name = "Bob", Slug = "bob" };
            var person3 = new Person { Name = "Charlie", Slug = "charlie" };

            db.Person.AddRange(person1, person2, person3);
            await db.SaveChangesAsync();

            // Create Gigs
            var gig1 = new Gig
            {
                Date = new DateOnly(2024, 1, 1),
                VenueId = venue.Id,
                Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }]
            };
            var gig2 = new Gig
            {
                Date = new DateOnly(2024, 1, 2),
                VenueId = venue.Id,
                Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }]
            };
            var gig3 = new Gig
            {
                Date = new DateOnly(2024, 1, 3),
                VenueId = venue.Id,
                Acts = [new GigArtist { ArtistId = artist.Id, IsHeadliner = true }]
            };

            db.Gig.AddRange(gig1, gig2, gig3);
            await db.SaveChangesAsync();

            // Create GigAttendees
            // Alice attended 2 gigs
            db.GigAttendee.Add(new GigAttendee { GigId = gig1.Id, PersonId = person1.Id });
            db.GigAttendee.Add(new GigAttendee { GigId = gig2.Id, PersonId = person1.Id });
            // Bob attended 1 gig
            db.GigAttendee.Add(new GigAttendee { GigId = gig1.Id, PersonId = person2.Id });
            // Charlie attended 0 gigs

            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetAll_ReturnsListOfAttendees()
    {
        await SeedData();

        var response = await _client.GetAsync("/api/attendees");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<GetAttendeeResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        
        var alice = result.FirstOrDefault(r => r.Name == "Alice");
        Assert.NotNull(alice);
        Assert.Equal(2, alice.GigCount);
        
        var bob = result.FirstOrDefault(r => r.Name == "Bob");
        Assert.NotNull(bob);
        Assert.Equal(1, bob.GigCount);
        
        var charlie = result.FirstOrDefault(r => r.Name == "Charlie");
        Assert.NotNull(charlie);
        Assert.Equal(0, charlie.GigCount);
    }

    [Fact]
    public async Task GetAll_WithCityFilter_ReturnsOnlyAttendeesWhoAttendedGigsInThatCity()
    {
        await SeedData();

        var response = await _client.GetAsync("/api/attendees?city=Test City");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<GetAttendeeResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Alice and Bob attended gigs, Charlie didn't
    }

    [Fact]
    public async Task GetAll_WithArtistFilter_ReturnsOnlyAttendeesWhoWentToThatArtist()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
            db.Venue.Add(venue);

            var artist1 = new Artist { Name = "Artist 1", Slug = "artist-1" };
            var artist2 = new Artist { Name = "Artist 2", Slug = "artist-2" };
            db.Artist.AddRange(artist1, artist2);

            var person1 = new Person { Name = "Person 1", Slug = "person-1" };
            var person2 = new Person { Name = "Person 2", Slug = "person-2" };
            db.Person.AddRange(person1, person2);
            await db.SaveChangesAsync();

            var gig1 = new Gig { VenueId = venue.Id, Date = new DateOnly(2024, 1, 1), Acts = [new GigArtist { ArtistId = artist1.Id, IsHeadliner = true }] };
            var gig2 = new Gig { VenueId = venue.Id, Date = new DateOnly(2024, 1, 2), Acts = [new GigArtist { ArtistId = artist2.Id, IsHeadliner = true }] };
            db.Gig.AddRange(gig1, gig2);
            await db.SaveChangesAsync();

            // Person 1 attended both gigs, Person 2 only attended artist 1
            db.GigAttendee.Add(new GigAttendee { GigId = gig1.Id, PersonId = person1.Id });
            db.GigAttendee.Add(new GigAttendee { GigId = gig2.Id, PersonId = person1.Id });
            db.GigAttendee.Add(new GigAttendee { GigId = gig1.Id, PersonId = person2.Id });
            await db.SaveChangesAsync();

            var response = await _client.GetAsync($"/api/attendees?artistId={artist1.Id}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<List<GetAttendeeResponse>>(_jsonOptions);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            var person1Result = result.FirstOrDefault(a => a.Name == "Person 1");
            Assert.NotNull(person1Result);
            Assert.Equal(1, person1Result.GigCount); // Only 1 gig with artist1 filter

            var person2Result = result.FirstOrDefault(a => a.Name == "Person 2");
            Assert.NotNull(person2Result);
            Assert.Equal(1, person2Result.GigCount);
        }
    }
}
