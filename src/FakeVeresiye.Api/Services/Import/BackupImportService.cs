using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Services.Import;

/// <summary>Outcome of a commit attempt, so the controller can pick the right status code.</summary>
public record ImportOutcome(bool Found, ValidationResultDto? Validation, ImportResultResponse? Result);

/// <summary>
/// Three-phase Veresiye 5 <c>.exa</c> import:
/// <list type="number">
///   <item>Preview — parse the file, stash it under a token, validate its structure. No writes.</item>
///   <item>Validate — re-check the stashed backup against the current database. No writes.</item>
///   <item>Import — re-validate, then commit the exact backup that was previewed, in one transaction.</item>
/// </list>
/// </summary>
public class BackupImportService(
    AppDbContext db,
    Veresiye5BackupReader reader,
    IPreviewStore previews,
    ILogger<BackupImportService> logger)
{
    private const string Source = "veresiye5";

    public async Task<ImportPreviewResponse> Preview(Stream file, string fileName)
    {
        var backup = reader.Read(file);
        var token = previews.Add(backup);
        var validation = await Validate(backup, db: null);

        return new ImportPreviewResponse(
            Token: token,
            FileName: fileName,
            Customers: backup.Customers.Count,
            Transactions: backup.Transactions.Count,
            TotalDebt: backup.Transactions.Sum(x => x.Debt),
            TotalPayments: backup.Transactions.Sum(x => x.Payment),
            Outstanding: backup.Transactions.Sum(x => x.Debt - x.Payment),
            Validation: validation,
            CustomerPreview: backup.Customers
                .Take(25)
                .Select(x => new CustomerPreviewItem(x.Id, x.Name, x.Phone))
                .ToList(),
            TransactionPreview: backup.Transactions
                .Take(50)
                .Select(x => new TransactionPreviewItem(
                    x.Id, x.CustomerId, x.Date, x.Type, x.Description, x.Debt, x.Payment))
                .ToList());
    }

    public async Task<ValidationResultDto?> Validate(Guid token)
    {
        if (!previews.TryGet(token, out var backup))
            return null;

        return await Validate(backup, db);
    }

    public async Task<ImportOutcome> Import(Guid token)
    {
        if (!previews.TryGet(token, out var backup))
            return new ImportOutcome(Found: false, Validation: null, Result: null);

        var validation = await Validate(backup, db);
        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Veresiye 5 import blocked by validation: {Errors}", string.Join(" | ", validation.Errors));
            return new ImportOutcome(Found: true, Validation: validation, Result: null);
        }

        await using var tx = await db.Database.BeginTransactionAsync();

        var customersById = new Dictionary<int, Customer>();
        foreach (var old in backup.Customers)
        {
            var externalId = old.Id.ToString();
            var customer = await db.Customers
                .FirstOrDefaultAsync(x => x.Source == Source && x.ExternalId == externalId);

            if (customer is null)
            {
                customer = new Customer
                {
                    Name = old.Name,
                    Phone = old.Phone,
                    Notes = old.Notes,
                    Source = Source,
                    ExternalId = externalId,
                };
                db.Customers.Add(customer);
            }

            customersById[old.Id] = customer;
        }

        await db.SaveChangesAsync();

        var imported = 0;
        foreach (var old in backup.Transactions)
        {
            if (!customersById.TryGetValue(old.CustomerId, out var customer))
                continue;

            var baseId = old.Id.ToString();

            if (old.Debt > 0)
                imported += await AddIfMissing(customer, TransactionType.Debt, old.Debt, old, baseId + ":debt");

            if (old.Payment > 0)
                imported += await AddIfMissing(customer, TransactionType.Payment, old.Payment, old, baseId + ":payment");

            // Preserve zero-amount historical records. With no amounts to go on, infer the
            // direction from the original Turkish transaction type.
            if (old.Debt == 0 && old.Payment == 0)
            {
                var isPayment = ContainsAny(old.Type, "tahsil", "ödeme", "odeme", "alacak");
                var type = isPayment ? TransactionType.Payment : TransactionType.Debt;
                var externalId = baseId + (isPayment ? ":payment:zero" : ":debt:zero");
                imported += await AddIfMissing(customer, type, 0m, old, externalId);
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        previews.Remove(token);

        logger.LogInformation(
            "Veresiye 5 import committed: {Customers} customers, {Imported}/{Source} transactions imported",
            backup.Customers.Count, imported, backup.Transactions.Count);

        return new ImportOutcome(
            Found: true,
            Validation: validation,
            Result: new ImportResultResponse(
                Customers: backup.Customers.Count,
                SourceTransactions: backup.Transactions.Count,
                ImportedTransactions: imported));
    }

    private async Task<int> AddIfMissing(
        Customer customer, TransactionType type, decimal amount, V5Transaction old, string externalId)
    {
        if (await db.Transactions.AnyAsync(x => x.Source == Source && x.ExternalId == externalId))
            return 0;

        db.Transactions.Add(new LedgerTransaction
        {
            CustomerId = customer.Id,
            Type = type,
            Amount = amount,
            Description = old.Description ?? old.Type,
            TransactionDate = old.Date,
            Source = Source,
            ExternalId = externalId,
        });
        return 1;
    }

    private static bool ContainsAny(string? value, params string[] needles) =>
        value is not null && needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static async Task<ValidationResultDto> Validate(V5Backup backup, AppDbContext? db)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (backup.Customers.Count == 0)
            errors.Add("No customers were found in the backup.");
        if (backup.Transactions.Count == 0)
            warnings.Add("No transactions were found in the backup.");

        var duplicateIds = backup.Customers
            .GroupBy(x => x.Id).Where(g => g.Count() > 1).Select(g => g.Key).Take(10).ToList();
        if (duplicateIds.Count > 0)
            errors.Add($"Duplicate customer IDs found: {string.Join(", ", duplicateIds)}.");

        var customerIds = backup.Customers.Select(x => x.Id).ToHashSet();
        var orphans = backup.Transactions.Count(x => !customerIds.Contains(x.CustomerId));
        if (orphans > 0)
            errors.Add($"{orphans:N0} transactions reference a customer that does not exist in the backup.");

        var emptyNames = backup.Customers.Count(x => string.IsNullOrWhiteSpace(x.Name));
        if (emptyNames > 0)
            errors.Add($"{emptyNames:N0} customers have an empty name.");

        var negatives = backup.Transactions.Count(x => x.Debt < 0 || x.Payment < 0);
        if (negatives > 0)
            errors.Add($"{negatives:N0} transactions contain negative debt/payment amounts.");

        var zeros = backup.Transactions.Count(x => x.Debt == 0 && x.Payment == 0);
        if (zeros > 0)
            warnings.Add(
                $"{zeros:N0} zero-amount transactions found. They have no financial effect but will be preserved as historical records.");

        if (db is not null)
        {
            var backupCustomerIds = backup.Customers.Select(c => c.Id.ToString()).ToList();
            var existingCustomers = await db.Customers.CountAsync(x =>
                x.Source == Source && x.ExternalId != null && backupCustomerIds.Contains(x.ExternalId));

            var externalIds = backup.Transactions
                .SelectMany(x => new[]
                {
                    x.Debt > 0 ? x.Id + ":debt" : null,
                    x.Payment > 0 ? x.Id + ":payment" : null,
                })
                .Where(x => x is not null)
                .ToList();
            var existingTransactions = externalIds.Count == 0
                ? 0
                : await db.Transactions.CountAsync(x =>
                    x.Source == Source && x.ExternalId != null && externalIds.Contains(x.ExternalId));

            if (existingCustomers > 0)
                warnings.Add(
                    $"{existingCustomers:N0} customers already exist from a previous Veresiye 5 import and will be reused.");
            if (existingTransactions > 0)
                warnings.Add($"{existingTransactions:N0} transaction records already exist and will be skipped.");
        }

        return ValidationResultDto.From(errors, warnings);
    }
}
