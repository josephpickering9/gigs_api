using Gigs.Models;
using Gigs.Types;
using Gigs.Utils;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services;

public class Database(DbContextOptions<Database> options): DbContext(options)
{
    public DbSet<Gig> Gig { get; set; }
    public DbSet<Artist> Artist { get; set; }
    public DbSet<Venue> Venue { get; set; }
    public DbSet<Festival> Festival { get; set; }
    public DbSet<FestivalAttendee> FestivalAttendee { get; set; }
    public DbSet<Person> Person { get; set; }
    public DbSet<Song> Song { get; set; }
    public DbSet<GigArtist> GigArtist { get; set; }
    public DbSet<GigArtistSong> GigArtistSong { get; set; }
    public DbSet<GigAttendee> GigAttendee { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Gig
        modelBuilder.Entity<Gig>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<Gig>()
            .Property(e => e.VenueId).HasGuidIdConversion();
        modelBuilder.Entity<Gig>()
            .Property(e => e.FestivalId).HasNullableGuidIdConversion();
        modelBuilder.Entity<Gig>()
            .Property(e => e.TicketType)
            .HasConversion(
                v => v.ToDescriptionString(),
                v => EnumExtensions.FromDescriptionString<TicketType>(v));
        modelBuilder.Entity<Gig>().HasIndex(b => b.Slug).IsUnique();

        // Artist
        modelBuilder.Entity<Artist>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<Artist>().HasIndex(b => b.Slug).IsUnique();

        // Venue
        modelBuilder.Entity<Venue>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<Venue>().HasIndex(b => b.Slug).IsUnique();

        // Festival
        modelBuilder.Entity<Festival>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<Festival>()
            .Property(e => e.VenueId).HasNullableGuidIdConversion();
        modelBuilder.Entity<Festival>().HasIndex(b => b.Slug).IsUnique();

        // Person
        modelBuilder.Entity<Person>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<Person>().HasIndex(b => b.Slug).IsUnique();

        // Song
        modelBuilder.Entity<Song>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<Song>()
            .Property(e => e.ArtistId).HasGuidIdConversion();
        modelBuilder.Entity<Song>().HasIndex(b => b.Slug).IsUnique();

        // GigArtist
        modelBuilder.Entity<GigArtist>()
            .Property(e => e.Id).HasGuidIdConversion().ValueGeneratedOnAdd();
        modelBuilder.Entity<GigArtist>()
            .Property(e => e.GigId).HasGuidIdConversion();
        modelBuilder.Entity<GigArtist>()
            .Property(e => e.ArtistId).HasGuidIdConversion();

        // GigArtistSong (Junction)
        modelBuilder.Entity<GigArtistSong>()
            .HasKey(t => new { t.GigArtistId, t.SongId });
        modelBuilder.Entity<GigArtistSong>()
            .Property(e => e.GigArtistId).HasGuidIdConversion();
        modelBuilder.Entity<GigArtistSong>()
            .Property(e => e.SongId).HasGuidIdConversion();
        modelBuilder.Entity<GigArtistSong>()
            .Property(e => e.WithArtistId).HasNullableGuidIdConversion();
        modelBuilder.Entity<GigArtistSong>()
            .Property(e => e.CoverArtistId).HasNullableGuidIdConversion();

        modelBuilder.Entity<GigArtistSong>()
            .HasOne(e => e.WithArtist)
            .WithMany()
            .HasForeignKey(e => e.WithArtistId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GigArtistSong>()
            .HasOne(e => e.CoverArtist)
            .WithMany()
            .HasForeignKey(e => e.CoverArtistId)
            .OnDelete(DeleteBehavior.Restrict);

        // GigAttendee (Junction)
        modelBuilder.Entity<GigAttendee>()
            .HasKey(t => new { t.GigId, t.PersonId });
        modelBuilder.Entity<GigAttendee>()
            .Property(e => e.GigId).HasGuidIdConversion();
        modelBuilder.Entity<GigAttendee>()
            .Property(e => e.PersonId).HasGuidIdConversion();

        
        // FestivalAttendee (Junction)
        modelBuilder.Entity<FestivalAttendee>()
            .HasKey(t => new { t.FestivalId, t.PersonId });
        modelBuilder.Entity<FestivalAttendee>()
            .Property(e => e.FestivalId).HasGuidIdConversion();
        modelBuilder.Entity<FestivalAttendee>()
            .Property(e => e.PersonId).HasGuidIdConversion();
    }
}
