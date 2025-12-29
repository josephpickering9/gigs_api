using Google.Apis.Auth.OAuth2;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gigs.Services.AI;

public class ImageSearchService
{
    private readonly ILogger<ImageSearchService> _logger;
    private readonly string? _searchEngineId;
    private readonly GoogleCredential? _credential;

    public ImageSearchService(IConfiguration configuration, ILogger<ImageSearchService> logger)
    {
        _logger = logger;
        _searchEngineId = configuration["GoogleCustomSearch:SearchEngineId"];

        // Use the same credentials as Vertex AI
        var credentialsJson = configuration["VertexAi:CredentialsJson"];
        var credentialsFile = configuration["VertexAi:CredentialsFile"];

        try
        {
            if (!string.IsNullOrWhiteSpace(credentialsJson) && credentialsJson.TrimStart().StartsWith("{"))
            {
                _logger.LogInformation("Using Vertex AI Service Account from JSON configuration for Custom Search.");
                _credential = GoogleCredential.FromJson(credentialsJson).CreateScoped("https://www.googleapis.com/auth/cse");
            }
            else if (!string.IsNullOrWhiteSpace(credentialsFile) && File.Exists(credentialsFile))
            {
                _logger.LogInformation("Using Vertex AI Service Account from File for Custom Search: {CredentialsFile}", credentialsFile);
                _credential = GoogleCredential.FromFile(credentialsFile).CreateScoped("https://www.googleapis.com/auth/cse");
            }
            else
            {
                _logger.LogInformation("Using Application Default Credentials (ADC) for Custom Search.");
                _credential = GoogleCredential.GetApplicationDefault().CreateScoped("https://www.googleapis.com/auth/cse");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load credentials for Custom Search. Image search will be disabled.");
            _credential = null;
        }
    }

    public async Task<string?> SearchConcertImageAsync(string artistName, string venueName, DateOnly date)
    {
        if (_credential == null || string.IsNullOrEmpty(_searchEngineId))
        {
            _logger.LogWarning("Google Custom Search not properly configured. Skipping image search.");
            return null;
        }

        try
        {
            var searchService = new CustomSearchAPIService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "GigsAPI"
            });

            // Build search query - prioritize stage/performance photos and exclude social media/tickets
            // Use Year + Venue + Artist instead of exact date to find broader gallery results
            var searchQuery = $"{artistName} {venueName} {date.Year} live concert -site:facebook.com -site:fbsbx.com -site:instagram.com -site:twitter.com -site:tiktok.com -site:ticketmaster.com -site:livenation.com";
            
            _logger.LogInformation("Searching for concert image: {SearchQuery}", searchQuery);

            var listRequest = searchService.Cse.List();
            listRequest.Cx = _searchEngineId;
            listRequest.Q = searchQuery;
            listRequest.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            listRequest.Num = 10; // Get more results to filter through
            listRequest.Safe = CseResource.ListRequest.SafeEnum.Active;

            var search = await listRequest.ExecuteAsync();

            if (search.Items != null && search.Items.Count > 0)
            {
                // Filter and rank images by quality indicators
                var rankedImages = search.Items
                    .Select(item => new
                    {
                        Url = item.Link,
                        Width = item.Image?.Width ?? 0,
                        Height = item.Image?.Height ?? 0,
                        Score = CalculateImageScore(item)
                    })
                    .Where(img => img.Width >= 800 && img.Height >= 600) // Minimum size requirement
                    .OrderByDescending(img => img.Score)
                    .ToList();

                if (rankedImages.Any())
                {
                    var bestImage = rankedImages.First();
                    _logger.LogInformation("Found concert image: {ImageUrl} (Score: {Score}, Size: {Width}x{Height})", 
                        bestImage.Url, bestImage.Score, bestImage.Width, bestImage.Height);
                    return bestImage.Url;
                }
            }

            _logger.LogInformation("No suitable images found for concert: {Artist} at {Venue} on {Date}", 
                artistName, venueName, date);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for concert image: {Artist} at {Venue}", 
                artistName, venueName);
            return null;
        }
    }

    public async Task<string?> SearchImageAsync(string query)
    {
        if (_credential == null || string.IsNullOrEmpty(_searchEngineId))
        {
            _logger.LogWarning("Google Custom Search not properly configured. Skipping image search for: {Query}", query);
            return null;
        }

        try
        {
            var searchService = new CustomSearchAPIService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "GigsAPI"
            });

            // Build search query - prioritize stage/performance photos and exclude social media
            var searchQuery = $"{query} -site:facebook.com -site:fbsbx.com -site:instagram.com -site:twitter.com -site:pinterest.com -site:tiktok.com";
            
            _logger.LogInformation("Searching for image: {Query}", searchQuery);

            var listRequest = searchService.Cse.List();
            listRequest.Cx = _searchEngineId;
            listRequest.Q = searchQuery;
            listRequest.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            listRequest.Num = 8; // Fetch more to filter down
            listRequest.Safe = CseResource.ListRequest.SafeEnum.Active;


            var search = await listRequest.ExecuteAsync();

            if (search.Items != null && search.Items.Count > 0)
            {
                // Filter and rank images by quality indicators
                var rankedImages = search.Items
                    .Select(item => new
                    {
                        Url = item.Link,
                        Width = item.Image?.Width ?? 0,
                        Height = item.Image?.Height ?? 0,
                        Score = CalculateImageScore(item)
                    })
                    // Minimum size requirement
                    .Where(img => img.Width >= 600 && img.Height >= 400) 
                    .OrderByDescending(img => img.Score)
                    .ToList();

                if (rankedImages.Any())
                {
                    var bestImage = rankedImages.First();
                    _logger.LogInformation("Found image: {ImageUrl} (Score: {Score}, Size: {Width}x{Height})", 
                        bestImage.Url, bestImage.Score, bestImage.Width, bestImage.Height);
                    return bestImage.Url;
                }
            }

            _logger.LogInformation("No suitable images found for query: {Query}", query);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for image: {Query}", query);
            return null;
        }
    }

    private int CalculateImageScore(Result imageResult)
    {
        var score = 0;
        var url = imageResult.Link?.ToLowerInvariant() ?? string.Empty;
        var title = imageResult.Title?.ToLowerInvariant() ?? string.Empty;

        // Prefer larger images
        var width = imageResult.Image?.Width ?? 0;
        var height = imageResult.Image?.Height ?? 0;
        score += (width * height) / 10000; // Size score

        // Prefer specific domains/sources known for quality concert photography
        if (url.Contains("gettyimages") || url.Contains("wireimage") || url.Contains("redferns") || url.Contains("wiki"))
            score += 100;
        
        // Prefer images with concert-related terms in title
        if (title.Contains("concert") || title.Contains("performance") || title.Contains("live") || title.Contains("band") || title.Contains("music"))
            score += 50;
        
        if (title.Contains("stage") || title.Contains("performs"))
            score += 30;

        // Penalize crowd/fan photos or social media that slipped through
        if (title.Contains("crowd") || title.Contains("audience") || url.Contains("user-generated") || 
            url.Contains("facebook") || url.Contains("fbsbx") || url.Contains("instagram") || url.Contains("twitter"))
            score -= 100;

        return score;
    }
}
