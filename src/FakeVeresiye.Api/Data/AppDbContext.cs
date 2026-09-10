using FakeVeresiye.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FakeVeresiye.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LedgerTransaction> Transactions => Set<LedgerTransaction>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // (Source, ExternalId) uniquely identifies an imported record, so re-importing
        // the same backup is idempotent.
        b.Entity<Customer>()
            .HasIndex(x => new { x.Source, x.ExternalId })
            .IsUnique();

        b.Entity<LedgerTransaction>()
            .HasIndex(x => new { x.Source, x.ExternalId })
            .IsUnique();

        b.Entity<Customer>()
            .HasMany(x => x.Transactions)
            .WithOne(x => x.Customer!)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<LedgerTransaction>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        b.Entity<LedgerTransaction>().Ignore(x => x.SignedAmount);
    }
}
