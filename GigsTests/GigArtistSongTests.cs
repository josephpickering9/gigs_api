using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GigsTests;

public class GigArtistSongTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public GigArtistSongTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CanSaveAndRetrieve_ExtendedGigArtistSongFields()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Database>();
            // Use a unique database name or ensure clean state
            // existing tests typically rely on in-memory sqlite or similar if configured
            // assuming CustomWebApplicationFactory uses a test db context (usually InMemory or Sqlite)
            
            // 1. Setup Data
            var artist = new Artist { Name = "Main Artist", Slug = "main-artist" };
            var withArtist = new Artist { Name = "Guest Artist", Slug = "guest-artist" };
            var coverArtist = new Artist { Name = "Original Artist", Slug = "original-artist" };
            var venue = new Venue { Name = "Test Venue", City = "Test City", Slug = "test-venue" };
            
            db.Artist.AddRange(artist, withArtist, coverArtist);
            db.Venue.Add(venue);
            await db.SaveChangesAsync();

            var song = new Song 
            { 
                Title = "Test Song", 
                ArtistId = artist.Id, 
                Slug = "test-song"
            };
            db.Song.Add(song);

            var gig = new Gig
            {
                VenueId = venue.Id,
                Date = DateOnly.FromDateTime(DateTime.Now),
                Slug = "test-gig",
                TicketType = TicketType.Standing
            };
            db.Gig.Add(gig);
            await db.SaveChangesAsync();

            var gigArtist = new GigArtist
            {
                GigId = gig.Id,
                ArtistId = artist.Id,
                IsHeadliner = true,
                Order = 0
            };
            db.GigArtist.Add(gigArtist);
            await db.SaveChangesAsync();

            // 2. Create GigArtistSong with new fields
            var gigArtistSong = new GigArtistSong
            {
                GigArtistId = gigArtist.Id,
                SongId = song.Id,
                Order = 1,
                IsEncore = true,
                Info = "Played with a broken string",
                IsTape = false,
                WithArtistId = withArtist.Id,
                CoverArtistId = coverArtist.Id
            };

            db.GigArtistSong.Add(gigArtistSong);
            await db.SaveChangesAsync();

            // 3. Clear Change Tracker to force reload
            db.ChangeTracker.Clear();

            // 4. Retrieve and Assert
            var retrieved = await db.GigArtistSong
                .Include(x => x.WithArtist)
                .Include(x => x.CoverArtist)
                .FirstOrDefaultAsync(x => x.GigArtistId == gigArtist.Id && x.SongId == song.Id);

            Assert.NotNull(retrieved);
            Assert.Equal("Played with a broken string", retrieved.Info);
            Assert.False(retrieved.IsTape);
            Assert.True(retrieved.IsEncore);
            Assert.Equal(withArtist.Id, retrieved.WithArtistId);
            Assert.Equal(coverArtist.Id, retrieved.CoverArtistId);
            Assert.NotNull(retrieved.WithArtist);
            Assert.Equal("Guest Artist", retrieved.WithArtist.Name);
            Assert.NotNull(retrieved.CoverArtist);
            Assert.Equal("Original Artist", retrieved.CoverArtist.Name);
        }
    }
}
