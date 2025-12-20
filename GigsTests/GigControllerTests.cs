using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gigs.DTOs;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GigsTests;

public class GigControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public GigControllerTests(CustomWebApplicationFactory<Program> factory)
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
            var venue1 = new Venue { Name = "Wembley Stadium", City = "London", Slug = "wembley" };
            var venue2 = new Venue { Name = "O2 Arena", City = "London", Slug = "o2" };
            db.Venue.AddRange(venue1, venue2);

            // Create Artists
            var artist1 = new Artist { Name = "Metallica", Slug = "metallica" };
            var artist2 = new Artist { Name = "Iron Maiden", Slug = "iron-maiden" };
            var artist3 = new Artist { Name = "Foo Fighters", Slug = "foo-fighters" };
            db.Artist.AddRange(artist1, artist2, artist3);

            // Create Person
            var person = new Person { Name = "John Doe", Slug = "john-doe" };
            db.Person.Add(person);

            await db.SaveChangesAsync();

            // Create Gigs
            var gigs = new List<Gig>();
            for (int i = 0; i < 15; i++)
            {
                var gig = new Gig
                {
                    VenueId = i % 2 == 0 ? venue1.Id : venue2.Id,
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(i)),
                    TicketType = TicketType.Standing,
                    Slug = $"gig-{i}"
                };

                // Add acts
                gig.Acts.Add(new GigArtist
                {
                    ArtistId = i % 3 == 0 ? artist1.Id : (i % 3 == 1 ? artist2.Id : artist3.Id),
                    IsHeadliner = true,
                    Order = 0
                });

                // Add attendee to some gigs
                if (i < 5)
                {
                    gig.Attendees.Add(new GigAttendee
                    {
                        PersonId = person.Id,
                        GigId = gig.Id
                    });
                }

                gigs.Add(gig);
            }

            db.Gig.AddRange(gigs);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetAll_ReturnsPaginatedResults()
    {
        await SeedData();

        var pageSize = 5;
        var response = await _client.GetAsync($"/api/gigs?page=1&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<GetGigResponse>>(_jsonOptions);
        
        Assert.NotNull(result);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(pageSize, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.TotalPages); // 15 / 5 = 3
    }

    [Fact]
    public async Task GetAll_Pagination_SecondPage()
    {
        await SeedData();
        
        var pageSize = 5;
        var response = await _client.GetAsync($"/api/gigs?page=2&pageSize={pageSize}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<GetGigResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task GetAll_Search_FiltersByVenue()
    {
        await SeedData();

        var search = "Wembley";
        var response = await _client.GetAsync($"/api/gigs?search={search}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<GetGigResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Items.All(g => g.VenueName.Contains("Wembley")));
        // Wembley is venue1, used for even indices: 0, 2, 4, 6, 8, 10, 12, 14 -> 8 gigs
        Assert.Equal(8, result.TotalCount);
    }

    [Fact]
    public async Task GetAll_Search_FiltersByArtist()
    {
        await SeedData();

        var search = "Metallica";
        var response = await _client.GetAsync($"/api/gigs?search={search}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<GetGigResponse>>(_jsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Items.All(g => g.Acts.Any(a => a.Name.Contains("Metallica"))));
        // Metallica is artist1, used when i % 3 == 0: 0, 3, 6, 9, 12 -> 5 gigs
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task GetAll_Filter_ByAttendee()
    {
        await SeedData();

        // Need to get the Person ID first, but I can't easily query it via API as there's no Person endpoint mentioned.
        // However, I know I seeded it. I can use a seeded ID if I hardcoded it or fetch it via a separate scope.
        // For simplicity, I'll fetch it from DB in a scope.
        PersonId personId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            personId = (await db.Person.FirstAsync()).Id;
        }

        var response = await _client.GetAsync($"/api/gigs?attendeeId={personId}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<GetGigResponse>>(_jsonOptions);

        Assert.NotNull(result);
        // We added attendee to gigs with index < 5 -> 5 gigs (0, 1, 2, 3, 4)
        Assert.Equal(5, result.TotalCount);
    }
}
