using System.Globalization;
using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Services.Customers;

public interface ICustomerService
{
    Task<PagedResponse<CustomerListItem>> List(int page, int pageSize, string? search);
    Task<CustomerDetailResponse?> Get(int id);
    Task<CustomerListItem> Create(CreateCustomerRequest request);

    /// <summary>Deletes the customer, if one exists with this id.</summary>
    /// <returns><c>false</c> if no such customer exists.</returns>
    Task<bool> Delete(int id);
}

public class CustomerService(AppDbContext db, ILogger<CustomerService> logger) : ICustomerService
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<PagedResponse<CustomerListItem>> List(int page, int pageSize, string? search)
    {
        var paging = PageRequest.From(page, pageSize);

        // SQLite's LIKE and ORDER BY only case-fold ASCII, so "çiçek" would not match "ÇİÇEK".
        // A shop's customer list is small and bounded, so filter/sort it in memory with a
        // Turkish-aware, accent- and case-insensitive comparison, then page the result.
        var all = await db.Customers
            .AsNoTracking()
            .Select(c => new CustomerListItem(
                c.Id,
                c.Name,
                c.Phone,
                c.Transactions.Sum(t => t.Type == TransactionType.Debt ? t.Amount : -t.Amount)))
            .ToListAsync();

        IEnumerable<CustomerListItem> matched = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            matched = all.Where(c => Turkish.CompareInfo.IndexOf(
                c.Name, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
        }

        var ordered = matched
            .OrderBy(c => c.Name, StringComparer.Create(Turkish, ignoreCase: true))
            .ToList();

        var items = ordered.Skip(paging.Skip).Take(paging.PageSize).ToList();
        return new PagedResponse<CustomerListItem>(items, paging.Page, paging.PageSize, ordered.Count);
    }

    public async Task<CustomerDetailResponse?> Get(int id) =>
        await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetailResponse(
                c.Id,
                c.Name,
                c.Phone,
                c.Notes,
                c.Transactions.Sum(t => t.Type == TransactionType.Debt ? t.Amount : -t.Amount),
                c.Transactions.Count))
            .FirstOrDefaultAsync();

    public async Task<CustomerListItem> Create(CreateCustomerRequest request)
    {
        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        logger.LogInformation("Customer {CustomerId} created: {Name}", customer.Id, customer.Name);
        return new CustomerListItem(customer.Id, customer.Name, customer.Phone, 0m);
    }

    public async Task<bool> Delete(int id)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
            return false;

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();

        logger.LogInformation("Customer {CustomerId} deleted: {Name}", customer.Id, customer.Name);
        return true;
    }
}
