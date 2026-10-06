using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Services.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace FakeVeresiye.Api.Controllers;

[ApiController]
[Route("api")]
public class TransactionsController(ITransactionService transactions) : ControllerBase
{
    [HttpGet("customers/{customerId:int}/transactions")]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> List(
        int customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] LedgerSort sort = LedgerSort.Date,
        [FromQuery] SortDirection dir = SortDirection.Desc)
    {
        var result = await transactions.List(customerId, page, pageSize, sort, dir);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("transactions/descriptions")]
    public async Task<ActionResult<List<string>>> ListDescriptions(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 8) =>
        Ok(await transactions.ListDescriptions(search, Math.Clamp(limit, 1, 50)));

    [HttpPost("customers/{customerId:int}/transactions")]
    public async Task<ActionResult<TransactionResponse>> Add(int customerId, CreateTransactionRequest request) =>
        ToActionResult(await transactions.Add(customerId, request));

    [HttpPut("transactions/{id:int}")]
    public async Task<ActionResult<TransactionResponse>> Update(int id, UpdateTransactionRequest request) =>
        ToActionResult(await transactions.Update(id, request));

    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> Delete(int id) =>
        await transactions.Delete(id) ? NoContent() : NotFound();

    private ActionResult<TransactionResponse> ToActionResult(TransactionWriteResult result) =>
        result.Error switch
        {
            TransactionWriteError.NotFound => NotFound(),
            TransactionWriteError.InvalidAmount => BadRequest("Amount must be positive."),
            _ => Ok(result.Transaction),
        };
}
