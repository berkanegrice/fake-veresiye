using System.ComponentModel.DataAnnotations;
using FakeVeresiye.Api.Models;

namespace FakeVeresiye.Api.Dtos;

/// <summary>Adds a new debt or payment to a customer.</summary>
public record CreateTransactionRequest(
    TransactionType Type,
    [Range(0.01, 100_000_000)] decimal Amount,
    DateTime? TransactionDate,
    [MaxLength(500)] string? Description);

/// <summary>
/// Edits an existing entry. The direction (<see cref="TransactionType"/>) cannot be changed;
/// only the amount, date and description are adjustable — including for entries dated in the past.
/// </summary>
public record UpdateTransactionRequest(
    [Range(0.01, 100_000_000)] decimal Amount,
    DateTime TransactionDate,
    [MaxLength(500)] string? Description);

public record TransactionResponse(
    int Id,
    string Type,
    decimal Amount,
    string? Description,
    DateTime TransactionDate);

/// <summary>Ledger sort field for <c>GET /api/customers/{id}/transactions</c>.</summary>
public enum LedgerSort
{
    Date,
    Amount,
    Type,
}

public enum SortDirection
{
    Asc,
    Desc,
}

public static class TransactionMapping
{
    public static TransactionResponse ToResponse(this LedgerTransaction t) =>
        new(t.Id, t.Type.ToString(), t.Amount, t.Description, t.TransactionDate);
}
