using Gigs.Types;

namespace Gigs.Services;

public interface IFileService
{
    Task<Result<string>> SaveFileAsync(byte[] file, string name = "image.png");
    Task<Result<string>> SaveFileAsync(IFormFile? file);
    void DeleteFile(string? filePath);
}
