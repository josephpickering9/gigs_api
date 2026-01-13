using Gigs.Services;
using Gigs.Models;
using Gigs.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using Gigs.Types;
using Moq;

namespace GigsTests;


public class FestivalServiceTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public FestivalServiceTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFestivals_SortedByStartDateDescending()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<FestivalService>();

        // Clear existing data
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        // Create Festivals
        var festival1 = new Festival { Name = "Festival A", Slug = "fest-a", StartDate = new DateOnly(2020, 1, 1) };
        var festival2 = new Festival { Name = "Festival B", Slug = "fest-b", StartDate = new DateOnly(2023, 1, 1) };
        var festival3 = new Festival { Name = "Festival C", Slug = "fest-c", StartDate = new DateOnly(2022, 1, 1) };
        var festival4 = new Festival { Name = "Festival D", Slug = "fest-d" }; // No date
        
        db.Festival.AddRange(festival1, festival2, festival3, festival4);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetAllAsync();
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count);
        Assert.Equal(festival2.Id, result[0].Id);
        Assert.Equal(festival3.Id, result[1].Id);
        Assert.Equal(festival1.Id, result[2].Id);
        Assert.Equal(festival4.Id, result[3].Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsGigs_SortedByDateAscending()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<FestivalService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var festival = new Festival { Name = "My Festival", Slug = "my-fest" };
        db.Festival.Add(festival);
        await db.SaveChangesAsync();

        var venue = new Venue { Name = "Venue", City = "City", Slug = "venue" };
        db.Venue.Add(venue);
        await db.SaveChangesAsync();

        var gig1 = new Gig { VenueId = venue.Id, FestivalId = festival.Id, Date = new DateOnly(2023, 6, 2), Slug="g1" };
        var gig2 = new Gig { VenueId = venue.Id, FestivalId = festival.Id, Date = new DateOnly(2023, 6, 1), Slug="g2" };
        var gig3 = new Gig { VenueId = venue.Id, FestivalId = festival.Id, Date = new DateOnly(2023, 6, 3), Slug="g3" };
        
        db.Gig.AddRange(gig1, gig2, gig3);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetByIdAsync(festival.Id);
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Gigs.Count);
        Assert.Equal(gig2.Id, result.Gigs[0].Id); // June 1st
        Assert.Equal(gig1.Id, result.Gigs[1].Id); // June 2nd
        Assert.Equal(gig3.Id, result.Gigs[2].Id); // June 3rd
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsGigs_SortedByDateAndOrder()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<FestivalService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var festival = new Festival { Name = "My Festival", Slug = "my-fest" };
        db.Festival.Add(festival);
        await db.SaveChangesAsync();

        var venue = new Venue { Name = "Venue", City = "City", Slug = "venue" };
        db.Venue.Add(venue);
        await db.SaveChangesAsync();

        // Same date, different order
        var gig1 = new Gig { VenueId = venue.Id, FestivalId = festival.Id, Date = new DateOnly(2023, 6, 1), Order = 2, Slug = "g1" };
        var gig2 = new Gig { VenueId = venue.Id, FestivalId = festival.Id, Date = new DateOnly(2023, 6, 1), Order = 1, Slug = "g2" };
        var gig3 = new Gig { VenueId = venue.Id, FestivalId = festival.Id, Date = new DateOnly(2023, 6, 1), Order = 3, Slug = "g3" };
        
        db.Gig.AddRange(gig1, gig2, gig3);
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetByIdAsync(festival.Id);
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Gigs.Count);
        Assert.Equal(gig2.Id, result.Gigs[0].Id); // Order 1
        Assert.Equal(gig1.Id, result.Gigs[1].Id); // Order 2
        Assert.Equal(gig3.Id, result.Gigs[2].Id); // Order 3
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCorrectDetails_DailyPriceAndAttendees()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Database>();
        var service = scope.ServiceProvider.GetRequiredService<FestivalService>();

        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var person = new Person { Name = "Attendee 1", Slug = "attendee-1" };
        db.Person.Add(person);
        await db.SaveChangesAsync();

        var festival = new Festival
        {
            Name = "Priced Festival",
            Slug = "priced-fest",
            StartDate = new DateOnly(2023, 6, 1),
            EndDate = new DateOnly(2023, 6, 3), // 3 days
            Price = 300
        };
        db.Festival.Add(festival);
        await db.SaveChangesAsync();

        db.FestivalAttendee.Add(new FestivalAttendee { FestivalId = festival.Id, PersonId = person.Id });
        await db.SaveChangesAsync();

        // Act
        var resultSource = await service.GetAllAsync();
        var result = resultSource.Data.FirstOrDefault();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(300, result.Price);
        Assert.Equal(100, result.DailyPrice); // 300 / 3 = 100
        Assert.Single(result.Attendees);
        Assert.Equal(person.Id, result.Attendees[0].Id);
    }

    [Fact]
    public async Task CreateAsync_CreatesFestivalWithDetails_AndAttendees()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FestivalService>();
        var db = scope.ServiceProvider.GetRequiredService<Database>();

        // Arrange
        var request = new Gigs.DataModels.UpsertFestivalRequest
        {
            Name = "New Festival",
            StartDate = new DateOnly(2024, 7, 1),
            EndDate = new DateOnly(2024, 7, 5),
            Price = 500,
            Attendees = ["New Attendee"]
        };

        // Act
        var resultSource = await service.CreateAsync(request);
        var result = resultSource.Data;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Festival", result.Name);
        Assert.Equal(new DateOnly(2024, 7, 1), result.StartDate);
        Assert.Equal(500, result.Price);
        Assert.Single(result.Attendees);
        Assert.Equal("New Attendee", result.Attendees[0].Name);

        // Verify DB
        var savedFestival = await db.Festival.Include(f => f.Attendees).FirstOrDefaultAsync(f => f.Id == result.Id);
        Assert.NotNull(savedFestival);
        Assert.Single(savedFestival.Attendees);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDetails_AndReconcilesAttendees()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FestivalService>();
        var db = scope.ServiceProvider.GetRequiredService<Database>();

        // Arrange
        var festival = new Festival { Name = "Old Festival", Slug = "old-fest" };
        db.Festival.Add(festival);
        var person = new Person { Name = "Existing Attendee", Slug = "existing" };
        db.Person.Add(person);
        await db.SaveChangesAsync();
        db.FestivalAttendee.Add(new FestivalAttendee { FestivalId = festival.Id, PersonId = person.Id });
        await db.SaveChangesAsync();

        var request = new Gigs.DataModels.UpsertFestivalRequest
        {
            Name = "Updated Festival",
            StartDate = new DateOnly(2025, 8, 1),
            Attendees = ["New Guy"] // Should remove "Existing Attendee" and add "New Guy"
        };

        // Act
        var resultSource = await service.UpdateAsync(festival.Id, request);
        var result = resultSource.Data;

        // Assert
        Assert.Equal("Updated Festival", result.Name);
        Assert.Equal(new DateOnly(2025, 8, 1), result.StartDate);
        Assert.Single(result.Attendees);
        Assert.Equal("New Guy", result.Attendees[0].Name);

        // Verify DB
        var savedFestival = await db.Festival.Include(f => f.Attendees).FirstOrDefaultAsync(f => f.Id == festival.Id);
        Assert.Single(savedFestival.Attendees);
        Assert.NotEqual(person.Id, savedFestival.Attendees[0].PersonId);
    }



    [Fact]
    public async Task EnrichFestivalAsync_ReturnsCandidates_AndDoesNotUpdateImageUrl()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        
        var db = services.GetRequiredService<Database>();
        var festivalRepo = services.GetRequiredService<FestivalRepository>();
        
        // Setup existing data
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        
        var festival = new Festival { Name = "Test Festival", Slug = "test-fest", Year = 2024 };
        await festivalRepo.AddAsync(festival);

        // Prepare Mocks for AiEnrichmentService constructor
        var mockLogger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<Gigs.Services.AI.AiEnrichmentService>>();
        var mockConfig = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c["VertexAi:ProjectId"]).Returns("test"); 

        // Mock the AiEnrichmentService
        var mockAi = new Moq.Mock<Gigs.Services.AI.AiEnrichmentService>(mockLogger.Object, mockConfig.Object, null, null);
        
        var candidates = new List<string> { "http://example.com/candidate1.jpg", "http://example.com/candidate2.jpg" };
        var aiResult = new Gigs.Services.AI.AiEnrichmentResult 
        { 
            ImageCandidates = candidates,
            ImageSearchQuery = "query"
        };
        
        mockAi.Setup(x => x.EnrichFestival(Moq.It.IsAny<Festival>()))
              .ReturnsAsync(aiResult.ToSuccess());
        
        // Manual FestivalService construction
        // We use real dependencies for everything except AI Service
        var service = new FestivalService(
            festivalRepo,
            services.GetRequiredService<GigService>(),
            services.GetRequiredService<PersonRepository>(),
            services.GetRequiredService<GigRepository>(),
            services.GetRequiredService<VenueRepository>(),
            mockAi.Object
        );

        // Act
        var result = await service.EnrichFestivalAsync(festival.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data.ImageCandidates);
        Assert.Equal(2, result.Data.ImageCandidates.Count);
        Assert.Equal("http://example.com/candidate1.jpg", result.Data.ImageCandidates[0]);
        Assert.Null(result.Data.ImageUrl); // Should NOT have applied it
        
        // Verify DB update
        var dbFestival = await festivalRepo.GetByIdAsync(festival.Id);
        Assert.Null(dbFestival.ImageUrl); // Should NOT be in DB
    }
}
