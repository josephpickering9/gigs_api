using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController(CsvImportService importService) : ControllerBase
{
    [HttpPost("csv")]
    public async Task<ActionResult<int>> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var result = await importService.ImportGigsAsync(file.OpenReadStream());
        
        // We can optionally wrap the int result in an object if the frontend expects it, 
        // but for now let's stick to standard Result behavior or map it manually if needed.
        // The previous code returned: new { Count = count, Message = ... }
        // Let's preserve that behavior for Success if possible, or just return the count as per the Result<int>.
        // Since we changed the signature to return Result<int>, .ToResponse() will return the int directly.
        // If we want the object, we should change the service to return Result<ImportResponseDto> or similar.
        // For simplicity and standardizing, let's return the Result.
        
        return result.ToResponse();
    }
}
