namespace FakeVeresiye.Api.Models;

/// <summary>Direction of a ledger entry. Values are stable and are sent over the wire.</summary>
public enum TransactionType
{
    Debt = 1,
    Payment = 2,
}
