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

            // Create Persons
            var person1 = new Person { Name = "Alice", Slug = "alice" };
            var person2 = new Person { Name = "Bob", Slug = "bob" };
            var person3 = new Person { Name = "Charlie", Slug = "charlie" };

            db.Person.AddRange(person1, person2, person3);
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
        Assert.Contains(result, r => r.Name == "Alice");
        Assert.Contains(result, r => r.Name == "Bob");
        Assert.Contains(result, r => r.Name == "Charlie");
    }
}
