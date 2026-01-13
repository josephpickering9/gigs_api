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

    public async Task<List<string>> SearchConcertImagesAsync(string artistName, string venueName, string cityName, DateOnly date)
    {
        if (_credential == null || string.IsNullOrEmpty(_searchEngineId))
        {
            _logger.LogWarning("Google Custom Search not properly configured. Skipping image search.");
            return [];
        }

        try
        {
            var searchService = new CustomSearchAPIService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credential,
                ApplicationName = "GigsAPI"
            });

            var searchQuery = $"{artistName} at {venueName} ({cityName}) {date:MMMM} {date.Year} live concert performance -site:facebook.com -site:fbsbx.com -site:instagram.com -site:twitter.com -site:pinterest.com -site:tiktok.com";

            _logger.LogInformation("Searching for concert images: {SearchQuery}", searchQuery);

            var listRequest = searchService.Cse.List();
            listRequest.Cx = _searchEngineId;
            listRequest.Q = searchQuery;
            listRequest.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            listRequest.Num = 10; // Get more results to filter through
            listRequest.Safe = CseResource.ListRequest.SafeEnum.Active;

            var search = await listRequest.ExecuteAsync();

            if (search.Items != null && search.Items.Count > 0)
            {
                // Debug logging: show all raw results before filtering
                _logger.LogInformation("API returned {Count} raw images before filtering", search.Items.Count);
                foreach (var item in search.Items)
                {
                    _logger.LogInformation("Raw image: {Url}, Size: {Width}x{Height}, Title: {Title}",
                        item.Link, item.Image?.Width ?? 0, item.Image?.Height ?? 0, item.Title);
                }

                // Filter and rank images by quality indicators
                var rankedImages = search.Items
                    .Select(item => new
                    {
                        Url = item.Link,
                        Width = item.Image?.Width ?? 0,
                        Height = item.Image?.Height ?? 0,
                        Score = CalculateImageScore(item, artistName)
                    })
                    .Where(img => img.Width >= 600 && img.Height >= 400)
                    .Where(img => !string.IsNullOrWhiteSpace(img.Url) &&
                                   !img.Url.StartsWith("x-raw-image", StringComparison.OrdinalIgnoreCase) &&
                                   img.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                   !IsSocialMediaUrl(img.Url))
                    .Take(10)
                    .Select(img => img.Url)
                    .ToList();

                if (rankedImages.Any())
                {
                    _logger.LogInformation("Found {Count} concert images for {Artist} at {Venue}", rankedImages.Count, artistName, venueName);
                    return rankedImages;
                }
            }

            _logger.LogInformation("No suitable images found for concert: {Artist} at {Venue} on {Date}",
                artistName, venueName, date);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for concert images: {Artist} at {Venue}",
                artistName, venueName);
            return [];
        }
    }

    public async Task<string?> SearchConcertImageAsync(string artistName, string venueName, string cityName, DateOnly date)
    {
        var images = await SearchConcertImagesAsync(artistName, venueName, cityName, date);
        return images.FirstOrDefault();
    }

    public async Task<List<string>> SearchImagesAsync(string query, int numResults = 8)
    {
         if (_credential == null || string.IsNullOrEmpty(_searchEngineId))
        {
            _logger.LogWarning("Google Custom Search not properly configured. Skipping image search for: {Query}", query);
            return [];
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

            _logger.LogInformation("Searching for images: {Query}", searchQuery);

            var listRequest = searchService.Cse.List();
            listRequest.Cx = _searchEngineId;
            listRequest.Q = searchQuery;
            listRequest.SearchType = CseResource.ListRequest.SearchTypeEnum.Image;
            listRequest.Num = numResults; // Fetch more to filter down
            listRequest.Safe = CseResource.ListRequest.SafeEnum.Active;


            var search = await listRequest.ExecuteAsync();

            if (search.Items != null && search.Items.Count > 0)
            {
                // Debug logging: show all raw results before filtering
                _logger.LogInformation("API returned {Count} raw images before filtering", search.Items.Count);
                foreach (var item in search.Items)
                {
                    _logger.LogInformation("Raw image: {Url}, Size: {Width}x{Height}, Title: {Title}",
                        item.Link, item.Image?.Width ?? 0, item.Image?.Height ?? 0, item.Title);
                }

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

                     .Where(img => !string.IsNullOrWhiteSpace(img.Url) &&
                                   !img.Url.StartsWith("x-raw-image", StringComparison.OrdinalIgnoreCase) && // Filter raw images
                                   img.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                                   !IsSocialMediaUrl(img.Url))
                    .ToList();

                if (rankedImages.Any())
                {
                    _logger.LogInformation("Found {Count} images for query {Query}", rankedImages.Count, query);
                    return rankedImages.Select(i => i.Url).ToList();
                }
            }

            _logger.LogInformation("No suitable images found for query: {Query}", query);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for images: {Query}", query);
            return [];
        }
    }

    public async Task<string?> SearchImageAsync(string query)
    {
        var images = await SearchImagesAsync(query);
        return images.FirstOrDefault();
    }

    private int CalculateImageScore(Result imageResult, string? artistName = null)
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

        // Prefer images explicitly mentioning the artist
        if (!string.IsNullOrWhiteSpace(artistName))
        {
            var lowerArtist = artistName.ToLowerInvariant();
            if (title.Contains(lowerArtist))
                score += 150;
            if (url.Contains(lowerArtist.Replace(" ", "")) || url.Contains(lowerArtist.Replace(" ", "-")))
                score += 150;
        }

        // Penalize crowd/fan photos or social media that slipped through
        if (title.Contains("crowd") || title.Contains("audience") || url.Contains("user-generated") ||
            url.Contains("facebook") || url.Contains("fbsbx") || url.Contains("instagram") || url.Contains("twitter"))
            score -= 100;

        // Prefer direct image links
        if (IsDirectImageUrl(url))
            score += 50;

        return score;
    }

    private bool IsSocialMediaUrl(string url)
    {
        var lowerUrl = url.ToLowerInvariant();
        var blockedDomains = new[]
        {
            "facebook.com", "fb.com", "fbcdn.net", "fbsbx.com",
            "instagram.com", "cdninstagram.com",
            "twitter.com", "twimg.com",
            "pinterest.com", "pinimg.com",
            "tiktok.com",
            "snapchat.com"
        };

        return blockedDomains.Any(domain => lowerUrl.Contains(domain));
    }

    private bool IsDirectImageUrl(string url)
    {
        var lowerUrl = url.ToLowerInvariant();
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        return imageExtensions.Any(ext => lowerUrl.EndsWith(ext));
    }
}
