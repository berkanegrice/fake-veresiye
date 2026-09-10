using System.Globalization;
using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController(AppDbContext db) : ControllerBase
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerListItem>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null)
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
        return Ok(new PagedResponse<CustomerListItem>(items, paging.Page, paging.PageSize, ordered.Count));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerDetailResponse>> Get(int id)
    {
        var response = await db.Customers
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

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerListItem>> Create(CreateCustomerRequest request)
    {
        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var item = new CustomerListItem(customer.Id, customer.Name, customer.Phone, 0m);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
            return NotFound();

        db.Customers.Remove(customer);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
