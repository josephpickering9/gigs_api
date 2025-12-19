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
        if (string.IsNullOrWhiteSpace(record.Venue) || string.IsNullOrWhiteSpace(record.City))
        {
             return; 
        }
        var venue = await GetOrCreateVenueAsync(record.Venue, record.City);

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
                Slug = Guid.NewGuid().ToString()
            };
            db.Gig.Add(gig);
        }

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

        db.GigArtist.RemoveRange(gig.Acts);
        gig.Acts.Clear();

        var order = 0;
        foreach (var actInfo in acts)
        {
            var artist = await GetOrCreateArtistAsync(actInfo.Name);
            var gigArtist = new GigArtist
            {
                Gig = gig,
                ArtistId = artist.Id,
                IsHeadliner = actInfo.IsHeadliner,
                Order = order++,
                SetlistUrl = actInfo.IsHeadliner ? actInfo.SetlistUrl : null
            };
            gigArtist.Artist = artist; 
            gig.Acts.Add(gigArtist);
        }

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
            venue = db.Venue.Local.FirstOrDefault(v => v.Name == name && v.City == city);
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
