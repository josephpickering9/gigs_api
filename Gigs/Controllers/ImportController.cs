using Gigs.Services;
using Gigs.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController(CsvImportService importService): ControllerBase
{
    [HttpPost("csv")]
    public async Task<ActionResult<int>> ImportCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var result = await importService.ImportGigsAsync(file.OpenReadStream());

        return result.ToResponse();
    }
}
