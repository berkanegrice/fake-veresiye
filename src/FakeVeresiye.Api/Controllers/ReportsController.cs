using System.Text;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Services.Statements;
using Microsoft.AspNetCore.Mvc;

namespace FakeVeresiye.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(
    IStatementService statements,
    StatementExcelExporter excelExporter,
    StatementPdfExporter pdfExporter) : ControllerBase
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// A customer's account statement between <c>from</c> and <c>to</c> (inclusive), one page of
    /// lines at a time. Totals and running balances still cover the whole window.
    /// </summary>
    [HttpGet("customers/{id:int}/statement")]
    public async Task<ActionResult<StatementPageResponse>> Statement(
        int id,
        DateOnly? from,
        DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var statement = await statements.BuildPageAsync(id, from, to, page, pageSize);
            return statement is null ? NotFound() : Ok(statement);
        }
        catch (ArgumentException e)
        {
            throw new BadHttpRequestException(e.Message);
        }
    }

    [HttpGet("customers/{id:int}/statement.xlsx")]
    public async Task<IActionResult> StatementExcel(int id, DateOnly? from, DateOnly? to)
    {
        var statement = await BuildAsync(id, from, to);
        if (statement is null)
            return NotFound();

        return File(excelExporter.Build(statement), ExcelContentType, FileName(statement, "xlsx"));
    }

    [HttpGet("customers/{id:int}/statement.pdf")]
    public async Task<IActionResult> StatementPdf(int id, DateOnly? from, DateOnly? to)
    {
        var statement = await BuildAsync(id, from, to);
        if (statement is null)
            return NotFound();

        return File(pdfExporter.Build(statement), "application/pdf", FileName(statement, "pdf"));
    }

    private async Task<StatementResponse?> BuildAsync(int id, DateOnly? from, DateOnly? to)
    {
        try
        {
            return await statements.BuildAsync(id, from, to);
        }
        catch (ArgumentException e)
        {
            throw new BadHttpRequestException(e.Message);
        }
    }

    private static string FileName(StatementResponse s, string extension)
    {
        var slug = new string(s.CustomerName
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-')
            .ToArray())
            .Trim()
            .Replace(' ', '-')
            .ToLowerInvariant();
        if (slug.Length == 0)
            slug = $"musteri-{s.CustomerId}";

        return $"ekstre-{slug}-{s.From:yyyyMMdd}-{s.To:yyyyMMdd}.{extension}";
    }
}
