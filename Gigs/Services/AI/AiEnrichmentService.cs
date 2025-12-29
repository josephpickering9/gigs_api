using System.Text.Json;
using Gigs.Models;
using Gigs.Services.SetlistFm;
using Gigs.Types;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gigs.Services.AI;

public class AiEnrichmentResult
{
    public List<string> SupportActs { get; set; } = [];
    public List<EnrichedSong> Setlist { get; set; } = [];
    public string? ImageSearchQuery { get; set; }
}

    public string Title { get; set; } = string.Empty;
    public bool IsEncore { get; set; }
    public string? Info { get; set; }
    public bool IsTape { get; set; }
    public string? WithArtistName { get; set; }
    public string? CoverArtistName { get; set; }
}

public class AiEnrichmentService
{
    private readonly ILogger<AiEnrichmentService> _logger;
    private readonly ImageSearchService _imageSearchService;
    private readonly SetlistFmService _setlistFmService;
    private readonly Lazy<PredictionServiceClient> _predictionServiceClient;
    private readonly string _projectId;
    private readonly string _location;
    private readonly string _publisher;
    private readonly string _model;

    public AiEnrichmentService(
        ILogger<AiEnrichmentService> logger, 
        IConfiguration configuration,
        ImageSearchService imageSearchService, 
        SetlistFmService setlistFmService)
    {
        _logger = logger;
        _imageSearchService = imageSearchService;
        _setlistFmService = setlistFmService;
        
        _projectId = configuration["VertexAi:ProjectId"] ?? throw new ArgumentNullException("VertexAi:ProjectId");
        _location = configuration["VertexAi:ModelLocation"] ?? "us-central1";
        _publisher = "google";
        _model = configuration["VertexAi:Model"] ?? "gemini-1.5-pro-001";

        var credentialsJson = configuration["VertexAi:CredentialsJson"];
        var credentialsFile = configuration["VertexAi:CredentialsFile"];

        _predictionServiceClient = new Lazy<PredictionServiceClient>(() =>
        {
            var builder = new PredictionServiceClientBuilder
            {
                Endpoint = $"{_location}-aiplatform.googleapis.com"
            };

            try
            {
                if (!string.IsNullOrWhiteSpace(credentialsJson) && credentialsJson.TrimStart().StartsWith("{"))
                {
                    _logger.LogInformation("Using Vertex AI Credentials from JSON configuration.");
                    var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credentialsJson);
                    builder.Credential = credential.CreateScoped(PredictionServiceClient.DefaultScopes);
                }
                else if (!string.IsNullOrWhiteSpace(credentialsFile) && File.Exists(credentialsFile))
                {
                    _logger.LogInformation("Using Vertex AI Credentials from File: {CredentialsFile}", credentialsFile);
                    var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(credentialsFile);
                    builder.Credential = credential.CreateScoped(PredictionServiceClient.DefaultScopes);
                }
                else
                {
                    _logger.LogInformation("Using Application Default Credentials (ADC) for Vertex AI.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Vertex AI credentials. Falling back to ADC.");
            }

            return builder.Build();
        });
    }

    public virtual async Task<Result<AiEnrichmentResult>> EnrichGig(Gig gig, bool enrichSetlist = true, bool enrichImage = true)
    {
        try
        {
            var headliner = gig.Acts.FirstOrDefault(a => a.IsHeadliner)?.Artist.Name;
            var venueName = gig.Venue.Name;
            var date = gig.Date;

            _logger.LogInformation("Enriching Gig {GigId}: {Artist} at {Venue} on {Date}. Setlist: {EnrichSetlist}, Image: {EnrichImage}", 
                gig.Id, headliner, venueName, date, enrichSetlist, enrichImage);

            if (string.IsNullOrEmpty(headliner))
            {
                return Result.Fail<AiEnrichmentResult>("Cannot enrich gig without a headliner.");
            }

            var result = new AiEnrichmentResult();

            if (enrichSetlist)
            {
                // 1. Get Setlist and Support Acts from Setlist.fm
                var setlistFmResult = await _setlistFmService.FindSetlistAsync(headliner, venueName, date);
                
                if (setlistFmResult != null)
                {
                    result.Setlist = setlistFmResult.Setlist;
                    result.SupportActs = setlistFmResult.SupportActs;
                }

                // 2. If no support acts found from Setlist.fm (or even if they were, maybe check AI?), try AI
                if (!result.SupportActs.Any())
                {
                    var aiSupportActs = await FindSupportActsWithAi(headliner, venueName, date, null);
                    if (aiSupportActs.Any())
                    {
                        result.SupportActs = aiSupportActs;
                    }
                }
            }

            if (enrichImage)
            {
                // 3. Search for a concert image using Custom Search API
                var imageUrl = await _imageSearchService.SearchConcertImageAsync(headliner, venueName, date);
                
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    result.ImageSearchQuery = imageUrl;
                }
            }

            return result.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching gig {GigId}", gig.Id);
            return Result.Fail<AiEnrichmentResult>($"Error enriching gig: {ex.Message}");
        }
    }

    public virtual async Task<Result<string>> EnrichArtistImage(string artistName)
    {
        try
        {
            // Search for a general artist image
            // Query: "{Artist Name} music artist" to be specific
            var imageUrl = await _imageSearchService.SearchImageAsync($"{artistName} music artist");

            if (string.IsNullOrWhiteSpace(imageUrl))
                return Result.NotFound<string>("Image not found.");

            return imageUrl.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching artist image for {ArtistName}", artistName);
            return Result.Fail<string>($"Error fetching artist image: {ex.Message}");
        }
    }

    public virtual async Task<Result<string>> EnrichVenueImage(string venueName, string city)
    {
        try
        {
            // Search for a venue image
            // Query: "{Venue Name} {City} venue"
            var imageUrl = await _imageSearchService.SearchImageAsync($"{venueName} {city} venue");

            if (string.IsNullOrWhiteSpace(imageUrl))
                return Result.NotFound<string>("Image not found.");

            return imageUrl.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching venue image for {VenueName} in {City}", venueName, city);
            return Result.Fail<string>($"Error fetching venue image: {ex.Message}");
        }
    }
    private async Task<List<string>> FindSupportActsWithAi(string artist, string venue, DateOnly date, string? contextInfo)
    {
         var endpoint = EndpointName.FormatProjectLocationPublisherModel(_projectId, _location, _publisher, _model);
         
         var prompt = $@"
You are a music historian data assistant.
I need to find the SUPPORT ACTS (opening bands) for a specific concert.

Concert Details:
- Headliner: {artist}
- Venue: {venue}
- Date: {date:yyyy-MM-dd}
{contextInfo}

TASK:
1. Search specifically for this concert to find who opened the show.
2. If multiple support acts played, list all of them in order of appearance if possible.
3. Be careful not to halluncinate. If you can't find any info, return 'null'.

Output ONLY a JSON array of strings. Example: [""Band A"", ""Band B""]
If none found, output: []
";

         var content = new Content
         {
             Role = "USER",
             Parts = { new Part { Text = prompt } }
         };

         var request = new GenerateContentRequest
         {
             Model = endpoint,
             Contents = { content },
             GenerationConfig = new GenerationConfig
             {
                 Temperature = 0.1f, // Low temperature for factual data
                 ResponseMimeType = "application/json"
             },
             Tools = { new Tool { GoogleSearch = new Tool.Types.GoogleSearch() } }
         };

         try
         {
             var response = await _predictionServiceClient.Value.GenerateContentAsync(request);
             var text = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text;
             
             if (string.IsNullOrWhiteSpace(text)) return [];
             
             // Clean code blocks
             text = text.Replace("```json", "").Replace("```", "").Trim();
             
             return JsonSerializer.Deserialize<List<string>>(text) ?? [];
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Error finding support acts with AI for {Artist} at {Venue}", artist, venue);
             return [];
         }
    }
}
