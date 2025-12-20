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
    private readonly ILogger<ImageService> _logger;

    public ImageService(IWebHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<ImageService> logger)
    {
        _env = hostEnvironment;
        _logger = logger;
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

    public async Task<string> SaveImageAsync(string fileName, byte[] data)
    {
        // Attempt optimization
        var imageToSave = data;
        
        if (!string.IsNullOrWhiteSpace(_tinifyApiKey))
        {
            var optimizationResult = await OptimiseImageAsync(data);
            if (optimizationResult.IsSuccess && optimizationResult.Data != null)
            {
                imageToSave = optimizationResult.Data;
            }
            else
            {
                _logger.LogWarning("Image optimization failed for {FileName}: {Error}. Saving original image.", fileName, optimizationResult.Error?.Message);
            }
        }

        var uploadDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads");
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        var filePath = Path.Combine(uploadDir, fileName);
        await File.WriteAllBytesAsync(filePath, imageToSave);
        
        return fileName;
    }
    public async Task<Result<int>> OptimiseAllImagesAsync()
    {
        if (string.IsNullOrWhiteSpace(_tinifyApiKey))
            return new Failure<int>("Tinify API key is not configured.");

        var uploadDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "uploads");
        if (!Directory.Exists(uploadDir))
            return new Failure<int>("Uploads directory does not exist.");

        var files = Directory.GetFiles(uploadDir, "*.*")
                             .Where(s => s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                         s.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                         s.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                         s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
        
        var count = 0;
        foreach (var file in files)
        {
            try 
            {
                var bytes = await File.ReadAllBytesAsync(file);
                // We just use the method we already have
                var result = await OptimiseImageAsync(bytes);
                if (result.IsSuccess)
                {
                    await File.WriteAllBytesAsync(file, result.Data!);
                    count++;
                }
                else
                {
                     _logger.LogWarning("Failed to optimise {File}: {Error}", Path.GetFileName(file), result.Error?.Message);
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error processing file {File}", Path.GetFileName(file));
            }
        }
        
        return new Success<int>(count);
    }
}

public class ImageData
{
    public required string FileName { get; init; }
    public required byte[] File { get; init; }
    public required string ContentType { get; init; }
}
