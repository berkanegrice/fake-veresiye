using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Services.Import;
using Microsoft.AspNetCore.Mvc;

namespace FakeVeresiye.Api.Controllers;

[ApiController]
[Route("api/import")]
[RequestSizeLimit(64 * 1024 * 1024)]
public class ImportController(
    BackupImportService backupImport,
    ExcelLedgerImporter excelImport) : ControllerBase
{
    /// <summary>Phase 1: parse the <c>.exa</c>, validate its structure, write nothing.</summary>
    [HttpPost("exa/preview")]
    public async Task<ActionResult<ImportPreviewResponse>> Preview(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "The selected file is empty." });

        if (!file.FileName.EndsWith(".exa", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Please select a Veresiye 5 .EXA backup." });

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await backupImport.PreviewAsync(stream, file.FileName));
        }
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }
    }

    /// <summary>Phase 2: re-validate the previewed backup against the current database.</summary>
    [HttpPost("exa/validate/{token:guid}")]
    public async Task<ActionResult<ValidationResultDto>> Validate(Guid token)
    {
        var result = await backupImport.ValidateAsync(token);
        return result is null
            ? NotFound(new { error = "Preview expired or was not found. Upload the backup again." })
            : Ok(result);
    }

    /// <summary>Phase 3: commit the exact backup that was previewed and validated.</summary>
    [HttpPost("exa/import/{token:guid}")]
    public async Task<IActionResult> Import(Guid token)
    {
        ImportOutcome outcome;
        try
        {
            outcome = await backupImport.ImportAsync(token);
        }
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }

        if (!outcome.Found)
            return NotFound(new { error = "Preview expired or was not found. Upload the backup again." });

        if (outcome.Result is null)
            return BadRequest(new { error = "Import blocked because validation failed.", validation = outcome.Validation });

        return Ok(outcome.Result);
    }

    /// <summary>Generic <c>.xlsx</c> ledger import, kept as a direct POC path.</summary>
    [HttpPost("excel")]
    public async Task<ActionResult<ExcelImportResultResponse>> Excel(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "The selected file is empty." });

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await excelImport.ImportAsync(stream));
        }
        catch (Exception e)
        {
            return BadRequest(new { error = e.Message });
        }
    }
}
