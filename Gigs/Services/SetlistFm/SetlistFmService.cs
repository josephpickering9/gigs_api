using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gigs.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gigs.Services.SetlistFm;

public class SetlistFmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SetlistFmService> _logger;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.setlist.fm/rest/1.0";

    public SetlistFmService(HttpClient httpClient, IConfiguration configuration, ILogger<SetlistFmService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["SetlistFm:ApiKey"] ?? string.Empty;

        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        }
    }

    public async Task<AiEnrichmentResult?> FindSetlistAsync(string artistName, string venueName, DateOnly date)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Setlist.fm API Key is missing. Skipping search.");
            return null;
        }

        try
        {
            // Setlist.fm expects date in dd-MM-yyyy format
            var dateString = date.ToString("dd-MM-yyyy");
            
            // Encode parameters
            var artistEncoded = Uri.EscapeDataString(artistName);
            // We usually search by Artist and Date first as it's most specific
            var requestUrl = $"{BaseUrl}/search/setlists?artistName={artistEncoded}&date={dateString}";

            _logger.LogInformation("Searching Setlist.fm: {Url}", requestUrl);

            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Setlist.fm search failed: {StatusCode} {Reason}", response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<SetlistSearchResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (searchResult?.Setlist == null || !searchResult.Setlist.Any())
            {
                _logger.LogInformation("No setlist found on Setlist.fm for {Artist} on {Date}", artistName, date);
                return null;
            }

            // Find the best match if multiple (though Artist + Date usually returns 1 or 0)
            // If venueName is provided, we could prioritize matches with similar venue names
            var match = searchResult.Setlist.FirstOrDefault(); 

            return ConvertToResult(match);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Setlist.fm for {Artist} on {Date}", artistName, date);
            return null;
        }
    }

    private AiEnrichmentResult ConvertToResult(SetlistFmSetlist? setlistData)
    {
        var result = new AiEnrichmentResult();

        if (setlistData == null) return result;

        // Extract songs
        if (setlistData.Sets?.Set != null)
        {
            foreach (var set in setlistData.Sets.Set)
            {
                var isEncore = set.Encore > 0;
                
                if (set.Song != null)
                {
                    foreach (var song in set.Song)
                    {
                        if (!string.IsNullOrWhiteSpace(song.Name))
                        {
                            result.Setlist.Add(new EnrichedSong
                            {
                                Title = song.Name,
                                IsEncore = isEncore,
                                Info = song.Info,
                                IsTape = song.Tape ?? false,
                                WithArtistName = song.With?.Name,
                                CoverArtistName = song.Cover?.Name
                            });
                        }
                    }
                }
            }
        }

        // Extract support acts
        if (!string.IsNullOrWhiteSpace(setlistData.Info))
        {
            // Regex for common patterns
            // "Support: A, B" or "Opener: A, B" or "with A, B"
            var info = setlistData.Info;
            
            // Try to match "Support: ..."
            var supportMatch = System.Text.RegularExpressions.Regex.Match(info, @"(?:Support|Opener|With):\s*(.*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (supportMatch.Success)
            {
                var supportString = supportMatch.Groups[1].Value;
                // Split by comma, but be careful of commas in band names if possible (hard without knowledge)
                // For now, simple comma split
                var acts = supportString.Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var act in acts)
                {
                     var trimmed = act.Trim();
                     // Filter out common noise
                     if (!string.IsNullOrWhiteSpace(trimmed) && trimmed.Length > 1)
                     {
                         result.SupportActs.Add(trimmed);
                     }
                }
            }
        }

        return result;
    }

    #region Data Models
    private class SetlistSearchResult
    {
        [JsonPropertyName("setlist")]
        public List<SetlistFmSetlist>? Setlist { get; set; }
    }

    private class SetlistFmSetlist
    {
        public string? Id { get; set; }
        public string? EventDate { get; set; }
        public SetlistFmArtist? Artist { get; set; }
        public SetlistFmVenue? Venue { get; set; }
        public SetlistFmSets? Sets { get; set; }
        public string? Info { get; set; }
    }

    private class SetlistFmArtist
    {
        public string? Name { get; set; }
    }

    private class SetlistFmVenue
    {
        public string? Name { get; set; }
        public SetlistFmCity? City { get; set; }
    }

    private class SetlistFmCity
    {
        public string? Name { get; set; }
    }

    private class SetlistFmSets
    {
        [JsonPropertyName("set")]
        public List<SetlistFmSet>? Set { get; set; }
    }

    private class SetlistFmSet
    {
        [JsonPropertyName("song")]
        public List<SetlistFmSong>? Song { get; set; }
        
        [JsonPropertyName("encore")]
        public int? Encore { get; set; }
    }

    private class SetlistFmSong
    {
        public string? Name { get; set; }
        public SetlistFmCover? Cover { get; set; }
        public SetlistFmWith? With { get; set; }
        public string? Info { get; set; }
        public bool? Tape { get; set; }
    }

    private class SetlistFmWith
    {
        public string? Name { get; set; }
    }

    private class SetlistFmCover
    {
        public string? Name { get; set; }
    }
    #endregion
}
