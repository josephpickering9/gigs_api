using Microsoft.AspNetCore.Mvc;
using Work_Experience_Search.Services.Image;

namespace Work_Experience_Search.Controllers;

[ApiController]
[Route("[controller]")]
public class MediaController(IImageService imageService) : ControllerBase
{
    [HttpGet("uploads/{fileName}")]
    public IActionResult GetFile(string fileName)
    {
        var imageData = imageService.GetImage(fileName);
        if (imageData.Data == null || !imageData.IsSuccess) return imageData.ToResponse();

        return File(imageData.Data.File, imageData.Data?.ContentType ?? "application/octet-stream");
    }
}
