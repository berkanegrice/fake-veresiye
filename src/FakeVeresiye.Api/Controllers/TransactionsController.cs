using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Controllers;

[ApiController]
[Route("api")]
public class TransactionsController(AppDbContext db) : ControllerBase
{
    [HttpGet("customers/{customerId:int}/transactions")]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> List(
        int customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] LedgerSort sort = LedgerSort.Date,
        [FromQuery] SortDirection dir = SortDirection.Desc)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId))
            return NotFound();

        var paging = PageRequest.From(page, pageSize);
        var query = db.Transactions.AsNoTracking().Where(t => t.CustomerId == customerId);
        var ascending = dir == SortDirection.Asc;

        var ordered = sort switch
        {
            LedgerSort.Amount => ascending
                ? query.OrderBy(t => t.Amount)
                : query.OrderByDescending(t => t.Amount),
            LedgerSort.Type => ascending
                ? query.OrderBy(t => t.Type)
                : query.OrderByDescending(t => t.Type),
            _ => ascending
                ? query.OrderBy(t => t.TransactionDate)
                : query.OrderByDescending(t => t.TransactionDate),
        };

        var total = await query.CountAsync();
        var items = await ordered
            .ThenByDescending(t => t.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(t => new TransactionResponse(
                t.Id, t.Type.ToString(), t.Amount, t.Description, t.TransactionDate))
            .ToListAsync();

        return Ok(new PagedResponse<TransactionResponse>(items, paging.Page, paging.PageSize, total));
    }

    [HttpPost("customers/{customerId:int}/transactions")]
    public async Task<ActionResult<TransactionResponse>> Add(int customerId, CreateTransactionRequest request)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId))
            return NotFound();

        if (request.Amount <= 0)
            return BadRequest("Amount must be positive.");

        var transaction = new LedgerTransaction
        {
            CustomerId = customerId,
            Type = request.Type,
            Amount = request.Amount,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TransactionDate = (request.TransactionDate ?? DateTime.UtcNow).Date,
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();

        return Ok(transaction.ToResponse());
    }

    /// <summary>
    /// Adjusts an existing entry, including one dated in the past. The direction (debt vs payment)
    /// is fixed; only amount, date and description change. The customer balance is a live sum, so
    /// it reflects the new value immediately.
    /// </summary>
    [HttpPut("transactions/{id:int}")]
    public async Task<ActionResult<TransactionResponse>> Update(int id, UpdateTransactionRequest request)
    {
        var transaction = await db.Transactions.FindAsync(id);
        if (transaction is null)
            return NotFound();

        if (request.Amount <= 0)
            return BadRequest("Amount must be positive.");

        transaction.Amount = request.Amount;
        transaction.TransactionDate = request.TransactionDate.Date;
        transaction.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await db.SaveChangesAsync();

        return Ok(transaction.ToResponse());
    }

    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var transaction = await db.Transactions.FindAsync(id);
        if (transaction is null)
            return NotFound();

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
