namespace FakeVeresiye.Api.Models;

/// <summary>A single debt or payment entry against a <see cref="Customer"/>.</summary>
public class LedgerTransaction
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateTime TransactionDate { get; set; }

    /// <summary>Identifier of this record in the system it was imported from, if any.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Name of the import source (e.g. "veresiye5"), or null when created in-app.</summary>
    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }

    /// <summary>Signed effect on the customer balance: debts add, payments subtract.</summary>
    public decimal SignedAmount => Type == TransactionType.Debt ? Amount : -Amount;
}
