namespace FakeVeresiye.Api.Dtos;

public record ValidationResultDto(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    bool IsValid)
{
    public static ValidationResultDto From(IEnumerable<string> errors, IEnumerable<string> warnings)
    {
        var e = errors.ToList();
        return new ValidationResultDto(e, warnings.ToList(), e.Count == 0);
    }
}

public record CustomerPreviewItem(int Id, string Name, string? Phone);

public record TransactionPreviewItem(
    int Id,
    int CustomerId,
    DateTime Date,
    string? Type,
    string? Description,
    decimal Debt,
    decimal Payment);

/// <summary>Result of phase 1: the backup has been parsed but nothing is written to the database.</summary>
public record ImportPreviewResponse(
    Guid Token,
    string FileName,
    int Customers,
    int Transactions,
    decimal TotalDebt,
    decimal TotalPayments,
    decimal Outstanding,
    ValidationResultDto Validation,
    IReadOnlyList<CustomerPreviewItem> CustomerPreview,
    IReadOnlyList<TransactionPreviewItem> TransactionPreview);

/// <summary>Result of phase 3: the previewed backup has been committed.</summary>
public record ImportResultResponse(int Customers, int SourceTransactions, int ImportedTransactions);

/// <summary>Result of the generic .xlsx import.</summary>
public record ExcelImportResultResponse(int Rows, int ImportedCustomers, int ImportedTransactions);
