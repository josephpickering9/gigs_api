using System.Text.Json;
using Gigs.Models;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace Gigs.Services.AI;

public interface IAiEnrichmentService
{
    Task<AiEnrichmentResult> EnrichGig(Gig gig);
    Task<string?> EnrichArtistImage(string artistName);
    Task<string?> EnrichVenueImage(string venueName, string city);
}

public class AiEnrichmentResult
{
    public List<string> SupportActs { get; set; } = [];
    public List<string> Setlist { get; set; } = [];
    public string? ImageSearchQuery { get; set; }
}

public class AiEnrichmentService : IAiEnrichmentService
{
    private readonly PredictionServiceClient _predictionServiceClient;
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

        var builder = new PredictionServiceClientBuilder
        {
            Endpoint = $"{_location}-aiplatform.googleapis.com"
        };

        if (!string.IsNullOrWhiteSpace(credentialsJson))
        {
            _logger.LogInformation("Using Vertex AI Credentials from JSON configuration.");
            var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credentialsJson);
            builder.Credential = credential.CreateScoped(PredictionServiceClient.DefaultScopes);
        }
        else if (!string.IsNullOrWhiteSpace(credentialsFile))
        {
            _logger.LogInformation("Using Vertex AI Credentials from File: {CredentialsFile}", credentialsFile);
             var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(credentialsFile);
             builder.Credential = credential.CreateScoped(PredictionServiceClient.DefaultScopes);
        }
        else
        {
            _logger.LogInformation("Using Application Default Credentials (ADC) for Vertex AI.");
        }
        
        _predictionServiceClient = builder.Build();
    }

    public async Task<AiEnrichmentResult> EnrichGig(Gig gig)
    {
        var endpoint = EndpointName.FormatProjectLocationPublisherModel(_projectId, _location, _publisher, _model);

        var prompt = $@"
You are a music historian helper.
I have a gig with the following details:
Artist: {gig.Acts.FirstOrDefault(a => a.IsHeadliner)?.Artist.Name ?? "Unknown"}
Venue: {gig.Venue.Name}
Date: {gig.Date}
Location: {gig.Venue.City}

Please provide the following information if available:
1. A list of likely support acts for this specific concert.
2. The likely setlist played by the headliner.
3. A specific search query I could use to find a high-quality likely photo of this specific concert (e.g. 'Artist Name Venue Name Date').

Output strictly in JSON format:
{{
  ""supportActs"": [""act1"", ""act2""],
  ""setlist"": [""song1"", ""song2""],
  ""imageSearchQuery"": ""query string""
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

        var instance = new Value
        {
            StructValue = new Struct
            {
                Fields =
                {
                    // Structuring for Gemini API input via PredictionServiceClient can be tricky essentially wrapping the Content
                    // However, for Gemini we typically use the GenerateContent methods if using the specialized Gemini client.
                    // But with PredictionServiceClient, we send raw instances. 
                    // Let's stick effectively to the raw JSON approach or switch to `Google.Cloud.AIPlatform.V1.GenerativeModel` if available in this package version.
                    // Actually, for simplicity and standard usage with `Google.Cloud.AIPlatform.V1`, we usually use `PredictionServiceClient` with the "generateContent" custom method or similar.
                    // A better approach for Gemini specifically in recent SDKs is effectively ensuring we send the right payload.
                    // BUT, `Google.Cloud.AIPlatform.V1` has a `GenerateContentRequest`.
                }
            }
        };
        
        // Wait, strictly speaking, for Gemini models on Vertex AI, we should use the `PredictionServiceClient.GenerateContentAsync` method which takes a `GenerateContentRequest`.
        
        var generateContentRequest = new GenerateContentRequest
        {
            Model = endpoint,
            Contents = { content },
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2f,
                MaxOutputTokens = 4096, // Increased to prevent truncation
                ResponseMimeType = "application/json"
            }
        };

        string? responseText = null;

        try
        {
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
            
            responseText = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text;
            
            if (string.IsNullOrEmpty(responseText))
            {
                _logger.LogWarning("Empty response from AI for Gig {GigId}", gig.Id);
                return new AiEnrichmentResult();
            }

            // Clean up: find first '{' and last '}'
            var firstBrace = responseText.IndexOf('{');
            var lastBrace = responseText.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                responseText = responseText.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
            else
            {
                // Fallback cleanup if braces not found correctly (unlikely for valid JSON)
                responseText = responseText.Replace("```json", "").Replace("```", "").Trim();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            
            var result = JsonSerializer.Deserialize<AiEnrichmentResult>(responseText, options);
            return result ?? new AiEnrichmentResult();
        }
        catch (Grpc.Core.RpcException ex) when (ex.Status.StatusCode == Grpc.Core.StatusCode.Unauthenticated || ex.Message.Contains("invalid_grant"))
        {
            _logger.LogError(ex, "Vertex AI Authentication failed. Please run 'gcloud auth application-default login' to refresh your credentials.");
            throw new Exception("Vertex AI Authentication failed. Please check server logs for details.", ex);
        }
        catch (JsonException ex)
        {
             _logger.LogError(ex, "Failed to parse AI response. Raw Text: {RawResponse}", responseText);
             throw new Exception($"Failed to parse AI response. Raw Text: {responseText}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching gig {GigId}", gig.Id);
            throw; // Or return empty/partial result depending on requirement
        }
    }

    public async Task<string?> EnrichArtistImage(string artistName)
    {
        var endpoint = EndpointName.FormatProjectLocationPublisherModel(_projectId, _location, _publisher, _model);

        var prompt = $@"
You are a music historian helper.
I have an artist named: {artistName}

Please provide a direct URL to a high-quality, representative image of this artist.
Prefer official press photos or album covers if possible.
Ensure the URL is likely to be valid and publicly accessible.
If you cannot find a specific URL, provide a URL to their Wikipedia page 'image' or a similar reliable source.
Actually, just try your best to give me a direct image link (ending in .jpg, .png etc).

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
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
            var url = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(url) || url.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching artist image for {ArtistName}", artistName);
            return null;
        }
    }

    public async Task<string?> EnrichVenueImage(string venueName, string city)
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
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
            var url = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(url) || url.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching venue image for {VenueName} in {City}", venueName, city);
            return null;
        }
    }
}
