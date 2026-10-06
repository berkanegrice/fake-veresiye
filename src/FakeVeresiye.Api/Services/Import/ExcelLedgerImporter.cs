using System.Globalization;
using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Services.Import;

/// <summary>
/// Imports a generic <c>.xlsx</c> ledger: rows are matched to in-app customers by name
/// (case-insensitive), creating them when absent, and their debt/payment cells become
/// ledger transactions. These records have no <c>Source</c>, so they are kept separate
/// from the Veresiye 5 import.
/// </summary>
public class ExcelLedgerImporter(AppDbContext db, GenericExcelImporter reader, ILogger<ExcelLedgerImporter> logger)
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<ExcelImportResultResponse> Import(Stream file)
    {
        var rows = reader.Read(file);
        var newCustomers = 0;
        var newTransactions = 0;

        // SQLite's LOWER() only case-folds ASCII, so "İstanbul" wouldn't match "istanbul".
        // The list of in-app customers is small and bounded, so match names in memory with
        // a Turkish-aware, case-insensitive comparison instead (same reasoning as the
        // customer search in CustomerService).
        var existing = await db.Customers.Where(c => c.Source == null).ToListAsync();

        foreach (var row in rows)
        {
            var name = row.Name.Trim();
            var customer = existing.FirstOrDefault(c =>
                Turkish.CompareInfo.Compare(c.Name, name, CompareOptions.IgnoreCase) == 0);

            if (customer is null)
            {
                customer = new Customer { Name = name, Phone = row.Phone };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
                existing.Add(customer);
                newCustomers++;
            }

            if (row.Debt > 0)
            {
                db.Transactions.Add(new LedgerTransaction
                {
                    CustomerId = customer.Id,
                    Type = TransactionType.Debt,
                    Amount = row.Debt,
                    Description = row.Description,
                    TransactionDate = row.Date,
                });
                newTransactions++;
            }

            if (row.Payment > 0)
            {
                db.Transactions.Add(new LedgerTransaction
                {
                    CustomerId = customer.Id,
                    Type = TransactionType.Payment,
                    Amount = row.Payment,
                    Description = row.Description,
                    TransactionDate = row.Date,
                });
                newTransactions++;
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Excel import committed: {Rows} rows, {NewCustomers} new customers, {NewTransactions} new transactions",
            rows.Count, newCustomers, newTransactions);
        return new ExcelImportResultResponse(rows.Count, newCustomers, newTransactions);
    }
}
