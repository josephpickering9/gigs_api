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
        
        // Assuming ADC is set up
        _predictionServiceClient = PredictionServiceClient.Create();
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
                MaxOutputTokens = 1024,
                ResponseMimeType = "application/json"
            }
        };

        try
        {
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
            
            var responseText = response.Candidates.FirstOrDefault()?.Content?.Parts.FirstOrDefault()?.Text;
            
            if (string.IsNullOrEmpty(responseText))
            {
                _logger.LogWarning("Empty response from AI for Gig {GigId}", gig.Id);
                return new AiEnrichmentResult();
            }

            // Clean up markdown code blocks if present
            responseText = responseText.Replace("```json", "").Replace("```", "").Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var result = JsonSerializer.Deserialize<AiEnrichmentResult>(responseText, options);
            return result ?? new AiEnrichmentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching gig {GigId}", gig.Id);
            throw; // Or return empty/partial result depending on requirement
        }
    }
}
