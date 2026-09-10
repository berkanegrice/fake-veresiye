using FakeVeresiye.Api.Services.Import;

namespace FakeVeresiye.Api.Tests;

public class BackupReaderTests
{
    private static V5Backup ReadSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.exa");
        using var stream = File.OpenRead(path);
        return new Veresiye5BackupReader().Read(stream);
    }

    [Fact]
    public void Reads_all_customers_and_transactions()
    {
        var backup = ReadSample();

        Assert.Equal(125, backup.Customers.Count);
        Assert.Equal(9232, backup.Transactions.Count);
    }

    [Fact]
    public void Decodes_turkish_characters_without_data_loss()
    {
        var backup = ReadSample();

        // The legacy database stores text as Windows-1254 bytes; a UTF-8 misread would
        // replace every Turkish letter with U+FFFD or '?'.
        Assert.DoesNotContain(backup.Customers, c => c.Name.Contains('�') || c.Name.Contains('?'));

        // "ÇİÇEK" (flower) / "ÇİÇEKÇİLİK" (florist) appears throughout this dataset.
        Assert.Contains(backup.Customers, c => c.Name.Contains("ÇİÇEK"));
        Assert.Contains(backup.Customers, c => c.Name.Contains("ŞTİ") || c.Name.Contains("GÖRDES"));
    }

    [Fact]
    public void Every_transaction_points_at_a_known_customer()
    {
        var backup = ReadSample();
        var ids = backup.Customers.Select(c => c.Id).ToHashSet();

        Assert.All(backup.Transactions, t => Assert.Contains(t.CustomerId, ids));
    }
}
