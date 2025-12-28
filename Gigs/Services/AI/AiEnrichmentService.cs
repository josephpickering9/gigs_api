using System.Text.Json;
using Gigs.Models;
using Gigs.Types;
using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace Gigs.Services.AI;

public class AiEnrichmentResult
{
    public List<string> SupportActs { get; set; } =[];
    public List<string> Setlist { get; set; } =[];
    public string? ImageSearchQuery { get; set; }
}

public class AiEnrichmentService
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

        _predictionServiceClient = builder.Build();
    }

    public virtual async Task<Result<AiEnrichmentResult>> EnrichGig(Gig gig)
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
                return Result.Fail<AiEnrichmentResult>("Empty response from AI.");
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

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var result = JsonSerializer.Deserialize<AiEnrichmentResult>(responseText, options);
            return (result ?? new AiEnrichmentResult()).ToSuccess();
        }
        catch (Grpc.Core.RpcException ex) when (ex.Status.StatusCode == Grpc.Core.StatusCode.Unauthenticated || ex.Message.Contains("invalid_grant"))
        {
            _logger.LogError(ex, "Vertex AI Authentication failed. Please run 'gcloud auth application-default login' to refresh your credentials.");
            return Result.Fail<AiEnrichmentResult>("Vertex AI Authentication failed.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI response. Raw Text: {RawResponse}", responseText);
            return Result.Fail<AiEnrichmentResult>($"Failed to parse AI response: {ex.Message}");
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
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
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
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
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
