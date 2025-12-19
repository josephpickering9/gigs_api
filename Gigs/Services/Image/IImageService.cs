using Gigs.Types;

namespace Gigs.Services.Image;

public interface IImageService
{
    Result<ImageData> GetImage(string fileName);
    Task<Result<byte[]>> OptimiseImageAsync(byte[] file);
}
