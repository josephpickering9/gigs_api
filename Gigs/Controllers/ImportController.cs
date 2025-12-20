using Gigs.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController(ICsvImportService importService) : ControllerBase
{
    [HttpPost("csv")]
    public async Task<IActionResult> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        try
        {
            var count = await importService.ImportGigsAsync(file.OpenReadStream());
            return Ok(new { Count = count, Message = $"Successfully imported {count} gigs." });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error importing CSV: {ex.Message}");
        }
    }
}
