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
        var processedGigIds = new HashSet<GigId>();

        foreach (var record in records)
        {
            if (record.Date == null || string.IsNullOrWhiteSpace(record.Headliner) || string.IsNullOrWhiteSpace(record.Venue))
            {
                continue;
            }

            try
            {
                await ProcessRecordAsync(record, processedGigIds);
                count++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing record {record.Headliner} @ {record.Venue}: {ex.Message}");
                throw;
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Reload all ends and try to apply changes? 
            // In a batch import, this is hard. We'll simply log detail and rethrow for now.
            foreach (var entry in ex.Entries)
            {
                var typeName = entry.Entity.GetType().Name;
                var key = entry.Metadata.FindPrimaryKey();
                var keyValues = key != null 
                    ? string.Join(", ", key.Properties.Select(p => $"{p.Name}={entry.Property(p.Name).CurrentValue}")) 
                    : "No Key";
                    
                Console.WriteLine($"Concurrency error on {typeName}. State: {entry.State}. Key: {keyValues}");
            }
            throw;
        }

        return count;
    }

    private async Task ProcessRecordAsync(CsvGigRecord record, HashSet<GigId> processedGigIds)
    {
        if (string.IsNullOrWhiteSpace(record.Venue) || string.IsNullOrWhiteSpace(record.City))
        {
             return; 
        }
        var venue = await GetOrCreateVenueAsync(record.Venue, record.City);

        // Check Local first to avoid duplicates in the same batch
        var gig = db.Gig.Local.FirstOrDefault(g => g.Date == record.Date && g.VenueId == venue.Id);

        if (gig == null)
        {
            gig = await db.Gig
            .Include(g => g.Acts)
            .Include(g => g.Attendees)
            .FirstOrDefaultAsync(g => g.Date == record.Date && g.VenueId == venue.Id);
        }

        if (gig == null)
        {
            gig = new Gig
            {
                Date = record.Date!.Value,
                VenueId = venue.Id,
                Slug = Guid.NewGuid().ToString()
            };
            db.Gig.Add(gig);
        }

        if (processedGigIds.Contains(gig.Id))
        {
            return;
        }
        processedGigIds.Add(gig.Id);

        gig.TicketCost = ParseCurrency(record.TicketCost);
        gig.TicketType = ParseTicketType(record.TicketType);

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

        // RECONCILE ACTS
        // 1. Identify acts to add/update
        var desiredArtists = new List<GigArtist>();
        var order = 0;
        foreach (var actInfo in acts)
        {
            var artist = await GetOrCreateArtistAsync(actInfo.Name);
            
            // Is this artist already in the gig?
            var existingGigArtist = gig.Acts.FirstOrDefault(a => a.ArtistId == artist.Id);
            
            if (existingGigArtist != null)
            {
                // Update existing
                existingGigArtist.IsHeadliner = actInfo.IsHeadliner;
                existingGigArtist.Order = order++;
                existingGigArtist.SetlistUrl = actInfo.IsHeadliner ? actInfo.SetlistUrl : existingGigArtist.SetlistUrl;
                desiredArtists.Add(existingGigArtist);
            }
            else
            {
                // Create new
                var newGigArtist = new GigArtist
                {
                    Gig = gig,
                    ArtistId = artist.Id,
                    Artist = artist,
                    IsHeadliner = actInfo.IsHeadliner,
                    Order = order++,
                    SetlistUrl = actInfo.IsHeadliner ? actInfo.SetlistUrl : null
                };
                gig.Acts.Add(newGigArtist);
                desiredArtists.Add(newGigArtist);
            }
        }

        // 2. Remove acts that are no longer present
        // We use ToList() to avoid modification errors during iteration if we were removing directly,
        // but here we just find the ones to remove.
        var actsToRemove = gig.Acts.Where(existing => !desiredArtists.Any(desired => desired.ArtistId == existing.ArtistId)).ToList();
        foreach (var toRemove in actsToRemove)
        {
            gig.Acts.Remove(toRemove);
            // Optional: explicity mark for deletion if it wasn't tracked as a collection change automatically 
            // (EF Core usually handles this if configured with Cascade delete or identifying relationships)
        }

        // RECONCILE ATTENDEES
        var desiredPeople = new List<Person>();
        if (!string.IsNullOrWhiteSpace(record.WentWith))
        {
            var peopleNames = SplitWithAmpersandKeep(record.WentWith);
            foreach (var name in peopleNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var person = await GetOrCreatePersonAsync(name);
                desiredPeople.Add(person);

                if (!gig.Attendees.Any(a => a.PersonId == person.Id))
                {
                    gig.Attendees.Add(new GigAttendee
                    {
                        Gig = gig,
                        Person = person
                    });
                }
            }
        }

        var attendeesToRemove = gig.Attendees.Where(a => !desiredPeople.Any(dp => dp.Id == a.PersonId)).ToList();
        foreach (var toRemove in attendeesToRemove)
        {
            gig.Attendees.Remove(toRemove);
        }
    }

    private async Task<Venue> GetOrCreateVenueAsync(string name, string city)
    {
        // Check Local case-insensitive
        var venue = db.Venue.Local.FirstOrDefault(v => 
            v.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase) && 
            v.City.Equals(city, StringComparison.InvariantCultureIgnoreCase));

        if (venue == null)
        {
            // Database is usually case-insensitive by default in SQL, but good to be consistent
            venue = await db.Venue.FirstOrDefaultAsync(v => v.Name == name && v.City == city);
        }

        if (venue == null)
        {
            venue = new Venue
            {
                Name = name,
                City = city,
                Slug = Guid.NewGuid().ToString()
            };
            db.Venue.Add(venue);
        }
        return venue;
    }

    private async Task<Artist> GetOrCreateArtistAsync(string name)
    {
        // Check Local case-insensitive
        var artist = db.Artist.Local.FirstOrDefault(a => a.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        if (artist == null)
        {
            artist = await db.Artist.FirstOrDefaultAsync(a => a.Name == name);
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
         // Check Local case-insensitive
        var person = db.Person.Local.FirstOrDefault(p => p.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        if (person == null)
        {
            person = await db.Person.FirstOrDefaultAsync(p => p.Name == name);
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
        var parts = input.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.ToList();
    }

    private decimal? ParseCurrency(string? cost)
    {
        if (string.IsNullOrWhiteSpace(cost)) return null;
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
