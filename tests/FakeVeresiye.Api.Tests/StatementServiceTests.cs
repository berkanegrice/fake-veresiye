using FakeVeresiye.Api.Data;
using FakeVeresiye.Api.Models;
using FakeVeresiye.Api.Services.Statements;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Tests;

public class StatementServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public StatementServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var customer = new Customer { Name = "AYŞE ÇİÇEK", Phone = "555" };
        customer.Transactions.AddRange(
        [
            Tx(TransactionType.Debt, 100m, "2023-01-10"),
            Tx(TransactionType.Payment, 30m, "2023-02-15"),
            Tx(TransactionType.Debt, 50m, "2023-06-01"),
            Tx(TransactionType.Payment, 20m, "2024-01-01"),
        ]);
        _db.Customers.Add(customer);
        _db.SaveChanges();
    }

    private static LedgerTransaction Tx(TransactionType type, decimal amount, string date) =>
        new() { Type = type, Amount = amount, TransactionDate = DateTime.Parse(date) };

    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public async Task Windowed_statement_carries_opening_and_running_balance()
    {
        var service = new StatementService(_db);

        var statement = await service.Build(1, D("2023-02-01"), D("2023-12-31"));

        Assert.NotNull(statement);
        Assert.Equal(100m, statement!.OpeningBalance);
        Assert.Equal(2, statement.Lines.Count);
        Assert.Equal(70m, statement.Lines[0].RunningBalance); // after the 30 payment
        Assert.Equal(120m, statement.Lines[1].RunningBalance); // after the 50 debt
        Assert.Equal(50m, statement.TotalDebt);
        Assert.Equal(30m, statement.TotalPayment);
        Assert.Equal(120m, statement.ClosingBalance);
    }

    [Fact]
    public async Task Default_range_covers_first_transaction_through_today()
    {
        var service = new StatementService(_db);

        var statement = await service.Build(1, from: null, to: null);

        Assert.NotNull(statement);
        Assert.Equal(0m, statement!.OpeningBalance);
        Assert.Equal(4, statement.Lines.Count);
        Assert.Equal(100m, statement.ClosingBalance); // 100 - 30 + 50 - 20
    }

    [Fact]
    public async Task Unknown_customer_returns_null()
    {
        var service = new StatementService(_db);
        Assert.Null(await service.Build(999, null, null));
    }

    [Fact]
    public async Task Paged_statement_slices_lines_but_keeps_window_totals()
    {
        var service = new StatementService(_db);

        var page1 = await service.BuildPage(1, from: null, to: null, page: 1, pageSize: 3);
        var page2 = await service.BuildPage(1, from: null, to: null, page: 2, pageSize: 3);

        Assert.NotNull(page1);
        Assert.Equal(4, page1!.LineTotal);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(3, page1.Lines.Count);
        Assert.Single(page2!.Lines);

        // Running balance is continuous across the page boundary, and window totals are unaffected.
        Assert.Equal(100m, page1.Lines[0].RunningBalance); // 0 + 100 (debt)
        Assert.Equal(70m, page1.Lines[1].RunningBalance); // - 30 (payment)
        Assert.Equal(120m, page1.Lines[2].RunningBalance); // + 50 (debt)
        Assert.Equal(100m, page2.Lines[0].RunningBalance); // - 20 (payment)
        Assert.Equal(100m, page2.ClosingBalance);
        Assert.Equal(150m, page1.TotalDebt);
        Assert.Equal(50m, page1.TotalPayment);
    }

    [Fact]
    public async Task Start_after_end_is_rejected()
    {
        var service = new StatementService(_db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Build(1, D("2024-01-01"), D("2023-01-01")));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
