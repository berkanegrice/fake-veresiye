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
public class ExcelLedgerImporter(AppDbContext db, GenericExcelImporter reader)
{
    public async Task<ExcelImportResultResponse> ImportAsync(Stream file)
    {
        var rows = reader.Read(file);
        var newCustomers = 0;
        var newTransactions = 0;

        foreach (var row in rows)
        {
            var name = row.Name.Trim();
            var customer = await db.Customers
                .FirstOrDefaultAsync(c => c.Source == null && c.Name.ToLower() == name.ToLower());

            if (customer is null)
            {
                customer = new Customer { Name = name, Phone = row.Phone };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
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
        return new ExcelImportResultResponse(rows.Count, newCustomers, newTransactions);
    }
}
