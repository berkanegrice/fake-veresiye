using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;

// Builds a small synthetic Veresiye 5 ".exa" backup: a real embedded SQLite database
// (CariKart/Data tables, text columns holding raw Windows-1254 bytes) wrapped in the
// same "marker + 8 filler bytes + int32 length-prefixed zlib chunk" container that
// Veresiye5BackupReader unpacks.
//
// Regenerates tests/FakeVeresiye.Api.Tests/fixtures/sample.exa — synthetic data only,
// so it's safe to commit and share, unlike a real customer backup. Run with:
//   dotnet run --project tools/FixtureGenerator -- tests/FakeVeresiye.Api.Tests/fixtures/sample.exa
// If the customer/transaction data below changes, update the counts asserted in
// BackupReaderTests to match.

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var turkish = Encoding.GetEncoding(1254);

var dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
try
{
    BuildEmbeddedDb(dbPath, turkish);

    var dbBytes = File.ReadAllBytes(dbPath);
    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        zlib.Write(dbBytes);
    var chunk = compressed.ToArray();

    const string prefix = @"c:\terkon\veresiye\data\";
    using var output = new MemoryStream();
    output.Write(Encoding.ASCII.GetBytes(prefix + "frm1.edb"));
    output.Write(new byte[8]); // filler the real reader skips unconditionally
    output.Write(BitConverter.GetBytes(chunk.Length));
    output.Write(chunk);

    var outPath = args.Length > 0 ? args[0] : "sample.exa";
    File.WriteAllBytes(outPath, output.ToArray());
    Console.WriteLine($"Wrote {outPath} ({output.Length} bytes)");
}
finally
{
    File.Delete(dbPath);
}

static void BuildEmbeddedDb(string path, Encoding turkish)
{
    File.Delete(path);
    using var connection = new SqliteConnection($"Data Source={path}");
    connection.Open();

    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE CariKart (ID INTEGER PRIMARY KEY, Unvan BLOB, Gsm BLOB, Tel BLOB, CNot BLOB);
            CREATE TABLE Data (
                ID INTEGER PRIMARY KEY, Tarih TEXT, Tur BLOB, Aciklama BLOB,
                Borc REAL, Alacak REAL, CariKartID INTEGER);
            """;
        cmd.ExecuteNonQuery();
    }

    var customers = new (int Id, string Name, string? Phone, string? Notes)[]
    {
        (1, "AYŞE ÇİÇEKÇİLİK", "5551112233", null),
        (2, "MEHMET DEMİR İNŞAAT ŞTİ", null, "Ödeme günü ayın 5'i"),
        (3, "GÖRDES MARKET", "5559998877", null),
    };

    foreach (var c in customers)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO CariKart (ID, Unvan, Gsm, Tel, CNot) VALUES ($id, $unvan, $gsm, $tel, $not)";
        cmd.Parameters.AddWithValue("$id", c.Id);
        cmd.Parameters.AddWithValue("$unvan", turkish.GetBytes(c.Name));
        cmd.Parameters.AddWithValue("$gsm", (object?)(c.Phone is null ? null : turkish.GetBytes(c.Phone)) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tel", DBNull.Value);
        cmd.Parameters.AddWithValue("$not", (object?)(c.Notes is null ? null : turkish.GetBytes(c.Notes)) ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    var transactions = new (int Id, int CustomerId, string Date, string Type, string? Description, double Debt, double Payment)[]
    {
        (1, 1, "2023-01-05 00:00:00", "Veresiye", "Çiçek buketi", 150.00, 0),
        (2, 1, "2023-01-20 00:00:00", "Tahsilat", null, 0, 100.00),
        (3, 2, "2023-02-01 00:00:00", "Veresiye", "İnşaat malzemesi", 5000.00, 0),
        (4, 2, "2023-02-15 00:00:00", "Ödeme", "Kısmi ödeme", 0, 2000.00),
        (5, 3, "2023-03-01 00:00:00", "Veresiye", "Market alışverişi", 320.50, 0),
        (6, 3, "2023-03-10 00:00:00", "Tahsilat", null, 0, 320.50),
    };

    foreach (var t in transactions)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Data (ID, Tarih, Tur, Aciklama, Borc, Alacak, CariKartID)
            VALUES ($id, $tarih, $tur, $aciklama, $borc, $alacak, $cariKartId)
            """;
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.Parameters.AddWithValue("$tarih", turkish.GetBytes(t.Date));
        cmd.Parameters.AddWithValue("$tur", turkish.GetBytes(t.Type));
        cmd.Parameters.AddWithValue("$aciklama", (object?)(t.Description is null ? null : turkish.GetBytes(t.Description)) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$borc", t.Debt);
        cmd.Parameters.AddWithValue("$alacak", t.Payment);
        cmd.Parameters.AddWithValue("$cariKartId", t.CustomerId);
        cmd.ExecuteNonQuery();
    }
}
