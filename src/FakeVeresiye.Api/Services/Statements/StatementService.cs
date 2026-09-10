using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Services.Statements;

public interface IStatementService
{
    /// <summary>
    /// Builds a customer's account statement over <paramref name="from"/>..<paramref name="to"/>
    /// (inclusive). <paramref name="from"/> defaults to the customer's first transaction date and
    /// <paramref name="to"/> to today. Returns <c>null</c> if the customer does not exist.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    Task<StatementResponse?> BuildAsync(int customerId, DateOnly? from, DateOnly? to);

    /// <summary>
    /// As <see cref="BuildAsync"/>, but returns only one page of the lines. Opening/closing
    /// balances, totals and each line's running balance still reflect the whole window.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    Task<StatementPageResponse?> BuildPageAsync(
        int customerId, DateOnly? from, DateOnly? to, int page, int pageSize);
}

public class StatementService(AppDbContext db) : IStatementService
{
    public async Task<StatementResponse?> BuildAsync(int customerId, DateOnly? from, DateOnly? to)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null)
            return null;

        var transactions = await db.Transactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync();

        var fromDate = from ?? (transactions.Count > 0
            ? DateOnly.FromDateTime(transactions[0].TransactionDate)
            : DateOnly.FromDateTime(DateTime.Today));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Today);

        if (fromDate > toDate)
            throw new ArgumentException("The start date must be on or before the end date.");

        var opening = transactions
            .Where(t => DateOnly.FromDateTime(t.TransactionDate) < fromDate)
            .Sum(t => t.SignedAmount);

        var inRange = transactions
            .Where(t =>
            {
                var d = DateOnly.FromDateTime(t.TransactionDate);
                return d >= fromDate && d <= toDate;
            })
            .ToList();

        var running = opening;
        var lines = new List<StatementLine>(inRange.Count);
        foreach (var t in inRange)
        {
            running += t.SignedAmount;
            lines.Add(new StatementLine(
                TransactionId: t.Id,
                Date: t.TransactionDate,
                Type: t.Type.ToString(),
                Description: t.Description,
                Debt: t.Type == TransactionType.Debt ? t.Amount : 0m,
                Payment: t.Type == TransactionType.Payment ? t.Amount : 0m,
                RunningBalance: running));
        }

        return new StatementResponse(
            CustomerId: customer.Id,
            CustomerName: customer.Name,
            CustomerPhone: customer.Phone,
            From: fromDate.ToDateTime(TimeOnly.MinValue),
            To: toDate.ToDateTime(TimeOnly.MinValue),
            OpeningBalance: opening,
            Lines: lines,
            TotalDebt: lines.Sum(l => l.Debt),
            TotalPayment: lines.Sum(l => l.Payment),
            ClosingBalance: running);
    }

    public async Task<StatementPageResponse?> BuildPageAsync(
        int customerId, DateOnly? from, DateOnly? to, int page, int pageSize)
    {
        var full = await BuildAsync(customerId, from, to);
        if (full is null)
            return null;

        var paging = PageRequest.From(page, pageSize);
        var pageLines = full.Lines.Skip(paging.Skip).Take(paging.PageSize).ToList();

        return new StatementPageResponse(
            CustomerId: full.CustomerId,
            CustomerName: full.CustomerName,
            CustomerPhone: full.CustomerPhone,
            From: full.From,
            To: full.To,
            OpeningBalance: full.OpeningBalance,
            TotalDebt: full.TotalDebt,
            TotalPayment: full.TotalPayment,
            ClosingBalance: full.ClosingBalance,
            Lines: pageLines,
            Page: paging.Page,
            PageSize: paging.PageSize,
            LineTotal: full.Lines.Count);
    }
}
