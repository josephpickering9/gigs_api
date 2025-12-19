using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using TinifyAPI;
using Gigs.Types;
using Gigs.Utils;

namespace Gigs.Services.Image;

public class ImageService : IImageService
{
    private readonly IWebHostEnvironment _env;
    private readonly string? _tinifyApiKey;

    public ImageService(IWebHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        _env = hostEnvironment;
        _tinifyApiKey = configuration["Tinify:ApiKey"];

        if (!string.IsNullOrWhiteSpace(_tinifyApiKey))
            Tinify.Key = _tinifyApiKey;
    }

    public Result<ImageData> GetImage(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads", fileName);

            if (!File.Exists(filePath))
                return new NotFoundFailure<ImageData>("Image not found");

            new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var contentType);

            var imageData = new ImageData
            {
                FileName = fileName,
                File = File.ReadAllBytes(filePath),
                ContentType = contentType ?? "application/octet-stream"
            };

            return new Success<ImageData>(imageData);
        }
        catch (Exception e)
        {
            return new Failure<ImageData>(e.Message);
        }
    }

    public async Task<Result<byte[]>> OptimiseImageAsync(byte[] image)
    {
        if (string.IsNullOrWhiteSpace(_tinifyApiKey))
            return new Failure<byte[]>("Tinify API key is not configured.");

        try
        {
            return new Success<byte[]>(await Tinify.FromBuffer(image).ToBuffer());
        }
        catch (TinifyException e)
        {
            return new Failure<byte[]>(e.Message);
        }
    }
}

public class ImageData
{
    public required string FileName { get; init; }
    public required byte[] File { get; init; }
    public required string ContentType { get; init; }
}
