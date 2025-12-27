using Gigs.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace Gigs.Services.External;

public class SpotifyService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpotifyService> _logger;
    private SpotifyClient? _spotify;

    public SpotifyService(IConfiguration configuration, ILogger<SpotifyService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private async Task EnsureAuthenticated()
    {
        if (_spotify != null) return;

        var clientId = _configuration["Spotify:ClientId"];
        var clientSecret = _configuration["Spotify:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            _logger.LogWarning("Spotify credentials not configured.");
            return;
        }

        try
        {
            var config = SpotifyClientConfig.CreateDefault();
            var request = new ClientCredentialsRequest(clientId, clientSecret);
            var response = await new OAuthClient(config).RequestToken(request);

            _spotify = new SpotifyClient(config.WithToken(response.AccessToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with Spotify.");
        }
    }

    public async Task<Result<string>> GetArtistImageAsync(string artistName)
    {
        await EnsureAuthenticated();

        if (_spotify == null) return Result.Fail<string>("Spotify authentication failed.");

        try
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Artist, artistName)
            {
                Limit = 1
            };

            var searchResponse = await _spotify.Search.Item(searchRequest);
            
            if (searchResponse.Artists.Items == null || !searchResponse.Artists.Items.Any())
            {
                _logger.LogInformation("No artist found on Spotify for '{ArtistName}'", artistName);
                return Result.NotFound<string>($"Artist '{artistName}' not found on Spotify.");
            }

            var artist = searchResponse.Artists.Items.First();
            var image = artist.Images.OrderByDescending(i => i.Width).FirstOrDefault();
            
            if (image?.Url == null) return Result.NotFound<string>("No image found for artist.");

            return image.Url.ToSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Spotify for artist '{ArtistName}'", artistName);
            return Result.Fail<string>($"Error searching Spotify: {ex.Message}");
        }
    }
}
