using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Gigs.DTOs;
using Gigs.Repositories;
using Gigs.Types;
using Gigs.Models;

namespace Gigs.Services;

public class CsvImportService(ArtistRepository artistRepository, GigService gigService)
{
    public async Task<Result<int>> ImportGigsAsync(Stream csvStream)
    {
        try
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

                try
                {
                    await ProcessRecordAsync(record);
                    count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing record {record.Headliner} @ {record.Venue}: {ex.Message}");
                    // throw; // Optionally suppress individual errors to allow partial import
                }
            }

            return count.ToSuccess();
        }
        catch (Exception ex)
        {
            return Result.Fail<int>($"Error importing gigs from CSV: {ex.Message}");
        }
    }

    private async Task ProcessRecordAsync(CsvGigRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Venue) || string.IsNullOrWhiteSpace(record.City))
        {
             return;
        }

        var actsRequest = new List<GigArtistRequest>();

        // 1. Headliner
        var headlinerId = await artistRepository.GetOrCreateAsync(record.Headliner!);
        actsRequest.Add(new GigArtistRequest
        {
            ArtistId = headlinerId,
            IsHeadliner = true,
            Order = 0,
            SetlistUrl = record.SetlistUrl
        });

        // 2. Support Acts
        if (!string.IsNullOrWhiteSpace(record.SupportActs))
        {
            var supports = SplitWithAmpersandKeep(record.SupportActs);
            var order = 1;
            foreach (var s in supports)
            {
                var supportId = await artistRepository.GetOrCreateAsync(s);
                actsRequest.Add(new GigArtistRequest
                {
                    ArtistId = supportId,
                    IsHeadliner = false,
                    Order = order++
                });
            }
        }

        // 3. Attendees
        var attendees = new List<string>();
        if (!string.IsNullOrWhiteSpace(record.WentWith))
        {
            attendees = SplitWithAmpersandKeep(record.WentWith);
        }

        var request = new UpsertGigRequest
        {
            VenueName = record.Venue,
            VenueCity = record.City,
            Date = record.Date!.Value,
            TicketCost = ParseCurrency(record.TicketCost),
            TicketType = ParseTicketType(record.TicketType),
            ImageUrl = null, // CSV doesn't seem to have image URL
            Acts = actsRequest,
            Attendees = attendees
        };

        await gigService.CreateAsync(request);
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
