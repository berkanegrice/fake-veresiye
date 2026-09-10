namespace FakeVeresiye.Api.Models;

/// <summary>A person who buys on credit ("veresiye") and carries a running balance.</summary>
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Notes { get; set; }

    /// <summary>Identifier of this record in the system it was imported from, if any.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Name of the import source (e.g. "veresiye5"), or null when created in-app.</summary>
    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<LedgerTransaction> Transactions { get; set; } = [];
}
