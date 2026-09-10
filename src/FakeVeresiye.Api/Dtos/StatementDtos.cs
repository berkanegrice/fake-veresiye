namespace FakeVeresiye.Api.Dtos;

/// <summary>One dated entry in a customer statement, with the balance after it was applied.</summary>
public record StatementLine(
    int TransactionId,
    DateTime Date,
    string Type,
    string? Description,
    decimal Debt,
    decimal Payment,
    decimal RunningBalance);

/// <summary>
/// A customer's account statement over <see cref="From"/>..<see cref="To"/> (inclusive),
/// with the balance carried in from before the window and the balance carried out.
/// Holds every line in the window — used by the Excel/PDF exporters.
/// </summary>
public record StatementResponse(
    int CustomerId,
    string CustomerName,
    string? CustomerPhone,
    DateTime From,
    DateTime To,
    decimal OpeningBalance,
    IReadOnlyList<StatementLine> Lines,
    decimal TotalDebt,
    decimal TotalPayment,
    decimal ClosingBalance);

/// <summary>
/// The same statement summary, but with only one page of <see cref="Lines"/>. Running balances
/// are still computed across the whole window, so a line's balance is correct on any page.
/// </summary>
public record StatementPageResponse(
    int CustomerId,
    string CustomerName,
    string? CustomerPhone,
    DateTime From,
    DateTime To,
    decimal OpeningBalance,
    decimal TotalDebt,
    decimal TotalPayment,
    decimal ClosingBalance,
    IReadOnlyList<StatementLine> Lines,
    int Page,
    int PageSize,
    int LineTotal)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(LineTotal / (double)PageSize);
}
