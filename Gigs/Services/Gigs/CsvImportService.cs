using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Gigs.Models;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services;

public class CsvImportService(Database db) : ICsvImportService
{
    public async Task<int> ImportGigsAsync(Stream csvStream)
    {
        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null
        });

        var records = csv.GetRecords<CsvGigRecord>();
        var count = 0;

        foreach (var record in records)
        {
            if (record.Date == null || string.IsNullOrWhiteSpace(record.Headliner) || string.IsNullOrWhiteSpace(record.Venue))
            {
                continue;
            }

            await ProcessRecordAsync(record);
            count++;
        }

        await db.SaveChangesAsync();
        return count;
    }

    private async Task ProcessRecordAsync(CsvGigRecord record)
    {
        // 1. Venue
        if (string.IsNullOrWhiteSpace(record.Venue) || string.IsNullOrWhiteSpace(record.City))
        {
            // Should verify if City is always present. 
            // If City missing, we can't create Venue properly as it is required.
            // For now, assuming validated record has Venue, but let's be safe.
             return; 
        }
        var venue = await GetOrCreateVenueAsync(record.Venue, record.City);

        // 2. Gig - Match by Date + Venue
        var gig = await db.Gig
            .Include(g => g.Acts)
            .Include(g => g.Attendees)
            .FirstOrDefaultAsync(g => g.Date == record.Date && g.VenueId == venue.Id);

        if (gig == null)
        {
            gig = new Gig
            {
                Date = record.Date!.Value,
                VenueId = venue.Id,
                Slug = Guid.NewGuid().ToString() // Temporary until refined logic if needed
            };
            db.Gig.Add(gig);
        }

        // Update details
        gig.TicketCost = ParseCurrency(record.TicketCost);
        gig.TicketType = ParseTicketType(record.TicketType);
        // Note: Genre is not on Gig model? "Genre" in CSV. Maybe ignored or added to Headliner? 
        // User didn't specify where Genre goes. The models don't have Genre on Gig/Artist. I'll ignore for now.

        // 3. Acts
        // Helper to split
        var acts = new List<(string Name, bool IsHeadliner, string? SetlistUrl)>();
        acts.Add((record.Headliner!, true, record.SetlistUrl));

        if (!string.IsNullOrWhiteSpace(record.SupportActs))
        {
            var supports = SplitWithAmpersandKeep(record.SupportActs);
            foreach (var s in supports)
            {
                acts.Add((s, false, null));
            }
        }

        // Clear existing acts to strict sync? Or append?
        // Since it's an import, replacing seems safer to avoid duplicates if run multiple times.
        // But EF ID tracking is tricky.
        db.GigArtist.RemoveRange(gig.Acts);
        gig.Acts.Clear();

        var order = 0;
        foreach (var actInfo in acts)
        {
            var artist = await GetOrCreateArtistAsync(actInfo.Name);
            var gigArtist = new GigArtist
            {
                Gig = gig, // EF will handle linkage needed for new entites? No, link object.
                ArtistId = artist.Id,
                IsHeadliner = actInfo.IsHeadliner,
                Order = order++,
                SetlistUrl = actInfo.IsHeadliner ? actInfo.SetlistUrl : null
            };
            // Note: If artist is new (Added to tracker but not saved), ID is temp.
            // Using navigation property 'Artist' is better if we have the entity track.
            gigArtist.Artist = artist; 
            gig.Acts.Add(gigArtist);
        }

        // 4. Attendees
        db.GigAttendee.RemoveRange(gig.Attendees);
        gig.Attendees.Clear();

        if (!string.IsNullOrWhiteSpace(record.WentWith))
        {
            var peopleNames = SplitWithAmpersandKeep(record.WentWith);
            foreach (var name in peopleNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var person = await GetOrCreatePersonAsync(name);
                var attendee = new GigAttendee
                {
                    Gig = gig,
                    Person = person
                };
                gig.Attendees.Add(attendee);
            }
        }
    }

    private async Task<Venue> GetOrCreateVenueAsync(string name, string city)
    {
        var venue = await db.Venue.FirstOrDefaultAsync(v => v.Name == name && v.City == city);
        if (venue == null)
        {
            // Check local tracker in case we added it in this batch
            venue = db.Venue.Local.FirstOrDefault(v => v.Name == name && v.City == city);
        }

        if (venue == null)
        {
            venue = new Venue
            {
                Name = name,
                City = city,
                Slug = Guid.NewGuid().ToString() // Placeholder
            };
            db.Venue.Add(venue);
        }
        return venue;
    }

    private async Task<Artist> GetOrCreateArtistAsync(string name)
    {
        var artist = await db.Artist.FirstOrDefaultAsync(a => a.Name == name);
        if (artist == null)
        {
            artist = db.Artist.Local.FirstOrDefault(a => a.Name == name);
        }

        if (artist == null)
        {
            artist = new Artist
            {
                Name = name,
                Slug = Guid.NewGuid().ToString()
            };
            db.Artist.Add(artist);
        }
        return artist;
    }

    private async Task<Person> GetOrCreatePersonAsync(string name)
    {
        var person = await db.Person.FirstOrDefaultAsync(p => p.Name == name);
        if (person == null)
        {
            person = db.Person.Local.FirstOrDefault(p => p.Name == name);
        }

        if (person == null)
        {
            person = new Person
            {
                Name = name,
                Slug = Guid.NewGuid().ToString()
            };
            db.Person.Add(person);
        }
        return person;
    }

    private List<string> SplitWithAmpersandKeep(string input)
    {
        // Split by ',' and '/'
        // Trim whitespace
        // "Miles Kane & The Vaccines" -> "Miles Kane & The Vaccines"
        var parts = input.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.ToList();
    }

    private decimal? ParseCurrency(string? cost)
    {
        if (string.IsNullOrWhiteSpace(cost)) return null;
        // Remove currency symbols and parse
        var clean = cost.Replace("£", "").Replace("$", "").Trim();
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
        {
            return val;
        }
        return null;
    }

    private TicketType ParseTicketType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return TicketType.Other;
        if (Enum.TryParse<TicketType>(type, true, out var result))
        {
            return result;
        }
        // Map from description if needed, logic is consistent with Enum
        return TicketType.Other;
    }

    public class CsvGigRecord
    {
        [Name("Date")]
        public DateOnly? Date { get; set; }

        [Name("Artist / Headliner")]
        public string? Headliner { get; set; }

        [Name("Support Acts")]
        public string? SupportActs { get; set; }

        [Name("Venue")]
        public string? Venue { get; set; }

        [Name("City")]
        public string? City { get; set; }

        [Name("Ticket Cost")]
        public string? TicketCost { get; set; }

        [Name("Ticket Type")]
        public string? TicketType { get; set; }

        [Name("Went With")]
        public string? WentWith { get; set; }

        [Name("Genre")]
        public string? Genre { get; set; }

        [Name("Setlist URL")]
        public string? SetlistUrl { get; set; }
    }
}
