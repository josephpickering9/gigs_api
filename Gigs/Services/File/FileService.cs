using Gigs.Types;
using Gigs.Utils;

namespace Gigs.Services;

public class FileService(IWebHostEnvironment hostEnvironment)
{
    public async Task<Result<string>> SaveFileAsync(byte[] file, string name = "image.png")
    {
        if (file.Length == 0) return new BadRequestFailure<string>("No file uploaded");

        var uploadDir = Path.Combine(hostEnvironment.WebRootPath ?? hostEnvironment.ContentRootPath, "uploads");
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var fileName = Guid.NewGuid() + FileExtensions.GetFileExtension(name);
        var filePath = Path.Combine(uploadDir, fileName);

        await File.WriteAllBytesAsync(filePath, file);

        return new Success<string>(filePath);
    }

    public async Task<Result<string>> SaveFileAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0) return new BadRequestFailure<string>("No file uploaded");

        var uploadDir = Path.Combine(hostEnvironment.WebRootPath ?? hostEnvironment.ContentRootPath, "uploads");
        if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadDir, fileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(fileStream);

        return new Success<string>(filePath);
    }

    public void DeleteFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (File.Exists(filePath)) File.Delete(filePath);
    }
}
