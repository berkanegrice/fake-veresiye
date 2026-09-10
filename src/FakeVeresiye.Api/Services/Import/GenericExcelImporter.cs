using System.Globalization;
using ClosedXML.Excel;

namespace FakeVeresiye.Api.Services.Import;

public record ImportRow(
    string Name,
    string? Phone,
    decimal Debt,
    decimal Payment,
    DateTime Date,
    string? Description);

/// <summary>
/// Reads a generic <c>.xlsx</c> ledger. Column order does not matter; headers are matched
/// case/space-insensitively against a set of English and Turkish aliases.
/// </summary>
public class GenericExcelImporter
{
    private static readonly string[] NameAliases =
        ["customer", "customername", "name", "fullname", "musteri", "musteriadi", "müşteri", "müşteriadı"];

    private static readonly string[] PhoneAliases =
        ["phone", "phonenumber", "telephone", "tel", "telefon", "gsm", "mobile"];

    private static readonly string[] DebtAliases =
        ["debt", "borc", "borç", "debit", "veresiye"];

    private static readonly string[] PaymentAliases =
        ["payment", "paid", "tahsilat", "alacak", "credit", "collection"];

    private static readonly string[] DateAliases =
        ["date", "transactiondate", "tarih", "işlemtarihi"];

    private static readonly string[] DescriptionAliases =
        ["description", "desc", "açıklama", "aciklama", "not", "notes"];

    private static readonly CultureInfo Turkish = new("tr-TR");

    public IReadOnlyList<ImportRow> Read(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("No worksheet.");
        var range = sheet.RangeUsed()
                    ?? throw new InvalidOperationException("Empty worksheet.");

        var rows = range.RowsUsed().ToList();
        if (rows.Count < 2)
            throw new InvalidOperationException("Need a header row and at least one data row.");

        var headers = rows[0].Cells().Select(c => Normalize(c.GetString())).ToList();
        int Column(string[] aliases) => headers.FindIndex(h => aliases.Any(a => h == Normalize(a)));

        var nameIndex = Column(NameAliases);
        if (nameIndex < 0)
            throw new InvalidOperationException("Could not detect a customer/name column.");

        int phoneIndex = Column(PhoneAliases),
            debtIndex = Column(DebtAliases),
            paymentIndex = Column(PaymentAliases),
            dateIndex = Column(DateAliases),
            descriptionIndex = Column(DescriptionAliases);

        var result = new List<ImportRow>();
        foreach (var row in rows.Skip(1))
        {
            var cells = row.Cells().ToList();
            string Get(int i) => i >= 0 && i < cells.Count ? cells[i].GetString().Trim() : "";

            var name = Get(nameIndex);
            if (name.Length == 0)
                continue;

            result.Add(new ImportRow(
                name,
                phoneIndex >= 0 ? Get(phoneIndex) : null,
                debtIndex >= 0 ? Money(Get(debtIndex)) : 0,
                paymentIndex >= 0 ? Money(Get(paymentIndex)) : 0,
                dateIndex >= 0 ? Date(cells[dateIndex]) : DateTime.UtcNow.Date,
                descriptionIndex >= 0 ? Get(descriptionIndex) : null));
        }

        return result;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

    private static decimal Money(string value) =>
        decimal.TryParse(
            value.Replace("₺", "").Replace("TL", "", StringComparison.OrdinalIgnoreCase),
            NumberStyles.Any,
            Turkish,
            out var parsed)
            ? parsed
            : 0;

    private static DateTime Date(IXLCell cell) =>
        cell.TryGetValue<DateTime>(out var value)
            ? value
            : DateTime.TryParse(cell.GetString(), Turkish, DateTimeStyles.None, out var parsed)
                ? parsed
                : DateTime.UtcNow.Date;
}
