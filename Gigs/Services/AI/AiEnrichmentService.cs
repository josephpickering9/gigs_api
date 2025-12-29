using System.Text.Json;
using Gigs.Models;
using Gigs.Types;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Value = Google.Protobuf.WellKnownTypes.Value;
using static Google.Cloud.AIPlatform.V1.Tool.Types;

namespace Gigs.Services.AI;

public class AiEnrichmentResult
{
    public List<string> SupportActs { get; set; } =[];
    public List<string> Setlist { get; set; } =[];
    public string? ImageSearchQuery { get; set; }
}

public class AiEnrichmentService
{
    private readonly Lazy<PredictionServiceClient> _predictionServiceClient;
    private readonly string _projectId;
    private readonly string _location;
    private readonly string _publisher;
    private readonly string _model;
    private readonly ILogger<AiEnrichmentService> _logger;

    public AiEnrichmentService(IConfiguration configuration, ILogger<AiEnrichmentService> logger)
    {
        _logger = logger;
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
                _logger.LogWarning(ex, "Failed to load Vertex AI credentials from configuration. Falling back to Application Default Credentials (ADC).");
            }

            return builder.Build();
        });
    }

    public virtual async Task<Result<AiEnrichmentResult>> EnrichGig(Gig gig)
    {
        var endpoint = EndpointName.FormatProjectLocationPublisherModel(_projectId, _location, _publisher, _model);

        var isFutureConcert = gig.Date.ToDateTime(TimeOnly.MinValue) > DateTime.UtcNow;
        
        var prompt = $@"
You are a concert data researcher with access to web search. Your job is to find COMPLETE and ACCURATE information.

Concert Information:
- Artist: {gig.Acts.FirstOrDefault(a => a.IsHeadliner)?.Artist.Name ?? "Unknown"}
- Venue: {gig.Venue.Name}
- Date: {gig.Date:yyyy-MM-dd}
- Location: {gig.Venue.City}
{(isFutureConcert ? "- NOTE: This concert is in the FUTURE and hasn't happened yet." : "")}

MANDATORY SEARCH TASKS:
{(isFutureConcert ? @"
1. SUPPORT ACTS: Search thoroughly for ALL announced support/opening acts for this show
   - Check multiple sources (official announcements, ticketing sites, venue websites)
   - Include ALL support acts, not just the main opener
   - Return empty array ONLY if you genuinely find no information

2. SETLIST: Search for typical setlists from this artist's current tour
   - Look for recent shows from the same tour
   - Return empty array if no tour setlist data is available

3. IMAGES: Find a promotional poster, tour image, or concert announcement image
   - Must be a direct image URL (.jpg, .png, .webp)
   - Try artist's official social media, venue announcements, tour posters
" : @"
1. SETLIST: **MUST search setlist.fm FIRST**
   - Search specifically: 'site:setlist.fm {gig.Acts.FirstOrDefault(a => a.IsHeadliner)?.Artist.Name} {gig.Venue.Name} {gig.Date:yyyy-MM-dd}'
   - If setlist.fm has the data, extract the COMPLETE setlist in order
   - Only return empty array if setlist.fm has no data for this specific show

2. SUPPORT ACTS: Find ALL opening acts for this specific show
   - Check setlist.fm, concert reviews, social media posts about the show
   - Include ALL support acts that performed, not just the main opener
   - Return empty array ONLY if you genuinely find no information after searching

3. CONCERT IMAGE: Find a photo from this specific concert
   - Search for: '{gig.Acts.FirstOrDefault(a => a.IsHeadliner)?.Artist.Name} {gig.Venue.Name} {gig.Date:yyyy-MM-dd} concert photo'
   - Must be a direct image URL (.jpg, .png, .webp)
   - Try concert review sites, photographer pages, fan photos
")}

CRITICAL RULES:
- Be THOROUGH - search multiple times with different queries if needed
- ALWAYS check setlist.fm for past concerts (it's the most reliable source)
- For images: ONLY return direct image URLs, never search queries or page URLs
- If you can't find data after thorough searching, return empty arrays/null
- Include ALL support acts you find, even if there are many

Output in JSON format:
{{
  ""supportActs"": [""all support acts in order""],
  ""setlist"": [""all songs in order""],
  ""imageSearchQuery"": ""direct image URL or null""
}}
";

        var content = new Content
        {
            Role = "USER",
            Parts =
            {
                new Part { Text = prompt }
            }
        };

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            Contents = { content },
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2f,
                MaxOutputTokens = 4096 // Increased to prevent truncation
            },
            Tools =
            {
                new Tool
                {
                    GoogleSearch = new GoogleSearch()
                }
            },
            ToolConfig = new ToolConfig
            {
                FunctionCallingConfig = new FunctionCallingConfig
                {
                    Mode = FunctionCallingConfig.Types.Mode.Any // Force tool usage
                }
            }
        };

        string? responseText = null;

        try
        {
            var response = await _predictionServiceClient.Value.GenerateContentAsync(generateContentRequest);

            // Log grounding metadata
            if (response.Candidates.Any())
            {
                var candidate = response.Candidates.First();
                if (candidate.GroundingMetadata != null)
                {
                    _logger.LogInformation("Grounding metadata for Gig {GigId}: {@GroundingMetadata}", gig.Id, candidate.GroundingMetadata);
                }
                else
                {
                    _logger.LogWarning("No grounding metadata found for Gig {GigId}", gig.Id);
                }
            }

            responseText = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text;

            _logger.LogInformation("AI response for Gig {GigId}: {ResponseLength} chars. Raw response: {RawResponse}", gig.Id, responseText?.Length ?? 0, responseText);

            if (string.IsNullOrEmpty(responseText))
            {
                _logger.LogWarning("Empty response from AI for Gig {GigId}. Response: {@Response}", gig.Id, response);
                return new AiEnrichmentResult().ToSuccess(); // Return empty result instead of failing
            }

            var firstBrace = responseText.IndexOf('{');
            var lastBrace = responseText.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                responseText = responseText.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
            else
            {
                responseText = responseText.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
            }

            _logger.LogInformation("Extracted JSON for Gig {GigId}: {Json}", gig.Id, responseText);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            try
            {
                var result = JsonSerializer.Deserialize<AiEnrichmentResult>(responseText, options);
                return (result ?? new AiEnrichmentResult()).ToSuccess();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI response as JSON for Gig {GigId}. Response was: {Response}. Returning empty result.", gig.Id, responseText);
                return new AiEnrichmentResult().ToSuccess(); // Return empty result instead of failing
            }
        }
        catch (Grpc.Core.RpcException ex) when (ex.Status.StatusCode == Grpc.Core.StatusCode.Unauthenticated || ex.Message.Contains("invalid_grant"))
        {
            _logger.LogError(ex, "Vertex AI Authentication failed. Please run 'gcloud auth application-default login' to refresh your credentials.");
            return Result.Fail<AiEnrichmentResult>("Vertex AI Authentication failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching gig {GigId}", gig.Id);
            return Result.Fail<AiEnrichmentResult>($"Error enriching gig: {ex.Message}");
        }
    }

    public virtual async Task<Result<string>> EnrichArtistImage(string artistName)
    {
        var endpoint = EndpointName.FormatProjectLocationPublisherModel(_projectId, _location, _publisher, _model);

        var prompt = $@"
You are a music historian helper.
I have an artist named: {artistName}

Please provide a direct URL to a high-quality, representative image of this artist.
Prefer official press photos or album covers if possible.
Ensure the URL is likely to be valid and publicly accessible.
Try your best to give me a direct image link (ending in .jpg, .png etc).

Output ONLY the URL. Do not output JSON. Just the raw URL string.
If you absolutely cannot find one, return 'null'.
";

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            Contents = { content },
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2f,
                MaxOutputTokens = 256,
                ResponseMimeType = "text/plain"
            }
        };

        try
        {
            var response = await _predictionServiceClient.Value.GenerateContentAsync(generateContentRequest);
            var url = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(url) || url.Equals("null", StringComparison.OrdinalIgnoreCase))
                return Result.NotFound<string>("Image not found.");

            return url.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching artist image for {ArtistName}", artistName);
            return Result.Fail<string>($"Error fetching artist image: {ex.Message}");
        }
    }

    public virtual async Task<Result<string>> EnrichVenueImage(string venueName, string city)
    {
        var endpoint = EndpointName.FormatProjectLocationPublisherModel(_projectId, _location, _publisher, _model);

        var prompt = $@"
You are a music venue expert.
I have a music venue named: {venueName} in {city}

Please provide a direct URL to a high-quality, representative image of this venue.
Prefer exterior or interior photos showing the venue clearly.
Ensure the URL is likely to be valid and publicly accessible.
Try your best to give me a direct image link (ending in .jpg, .png etc).

Output ONLY the URL. Do not output JSON. Just the raw URL string.
If you absolutely cannot find one, return 'null'.
";

        var content = new Content
        {
            Role = "USER",
            Parts = { new Part { Text = prompt } }
        };

        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            Contents = { content },
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2f,
                MaxOutputTokens = 256,
                ResponseMimeType = "text/plain"
            }
        };

        try
        {
            var response = await _predictionServiceClient.Value.GenerateContentAsync(generateContentRequest);
            var url = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(url) || url.Equals("null", StringComparison.OrdinalIgnoreCase))
                return Result.NotFound<string>("Image not found.");

            return url.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching venue image for {VenueName} in {City}", venueName, city);
            return Result.Fail<string>($"Error fetching venue image: {ex.Message}");
        }
    }
}
