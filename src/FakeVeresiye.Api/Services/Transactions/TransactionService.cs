using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Services.Transactions;

/// <summary>Outcome of adding or editing a transaction: which precondition failed, if any.</summary>
public enum TransactionWriteError
{
    None,
    NotFound,
    InvalidAmount,
}

public record TransactionWriteResult(TransactionWriteError Error, TransactionResponse? Transaction)
{
    public static TransactionWriteResult Ok(TransactionResponse t) => new(TransactionWriteError.None, t);
    public static TransactionWriteResult Failed(TransactionWriteError error) => new(error, null);
}

public interface ITransactionService
{
    Task<PagedResponse<TransactionResponse>?> List(
        int customerId, int page, int pageSize, LedgerSort sort, SortDirection dir);

    Task<TransactionWriteResult> Add(int customerId, CreateTransactionRequest request);
    Task<TransactionWriteResult> Update(int id, UpdateTransactionRequest request);

    /// <returns><c>false</c> if no such transaction exists.</returns>
    Task<bool> Delete(int id);
}

public class TransactionService(AppDbContext db, ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<PagedResponse<TransactionResponse>?> List(
        int customerId, int page, int pageSize, LedgerSort sort, SortDirection dir)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId))
            return null;

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

        return new PagedResponse<TransactionResponse>(items, paging.Page, paging.PageSize, total);
    }

    public async Task<TransactionWriteResult> Add(int customerId, CreateTransactionRequest request)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId))
            return TransactionWriteResult.Failed(TransactionWriteError.NotFound);

        if (request.Amount <= 0)
            return TransactionWriteResult.Failed(TransactionWriteError.InvalidAmount);

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

        logger.LogInformation(
            "Transaction {TransactionId} added for customer {CustomerId}: {Type} {Amount}",
            transaction.Id, transaction.CustomerId, transaction.Type, transaction.Amount);
        return TransactionWriteResult.Ok(transaction.ToResponse());
    }

    /// <summary>
    /// Adjusts an existing entry, including one dated in the past. The direction (debt vs payment)
    /// is fixed; only amount, date and description change. The customer balance is a live sum, so
    /// it reflects the new value immediately.
    /// </summary>
    public async Task<TransactionWriteResult> Update(int id, UpdateTransactionRequest request)
    {
        var transaction = await db.Transactions.FindAsync(id);
        if (transaction is null)
            return TransactionWriteResult.Failed(TransactionWriteError.NotFound);

        if (request.Amount <= 0)
            return TransactionWriteResult.Failed(TransactionWriteError.InvalidAmount);

        transaction.Amount = request.Amount;
        transaction.TransactionDate = request.TransactionDate.Date;
        transaction.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Transaction {TransactionId} updated for customer {CustomerId}: {Type} {Amount}",
            transaction.Id, transaction.CustomerId, transaction.Type, transaction.Amount);
        return TransactionWriteResult.Ok(transaction.ToResponse());
    }

    public async Task<bool> Delete(int id)
    {
        var transaction = await db.Transactions.FindAsync(id);
        if (transaction is null)
            return false;

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Transaction {TransactionId} deleted for customer {CustomerId}: {Type} {Amount}",
            transaction.Id, transaction.CustomerId, transaction.Type, transaction.Amount);
        return true;
    }
}
