using Gigs.Services.Image;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("[controller]")]
public class MediaController(ImageService imageService) : ControllerBase
{
    [HttpGet("uploads/{fileName}")]
    public IActionResult GetFile(string fileName)
    {
        var imageData = imageService.GetImage(fileName);
        if (imageData.Data == null || !imageData.IsSuccess) return imageData.ToResponse();

        return File(imageData.Data.File, imageData.Data?.ContentType ?? "application/octet-stream");
    }
}
