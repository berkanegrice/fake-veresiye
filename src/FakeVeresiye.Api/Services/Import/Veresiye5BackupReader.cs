using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;

namespace FakeVeresiye.Api.Services.Import;

public record V5Customer(int Id, string Name, string? Phone, string? Notes);

public record V5Transaction(
    int Id,
    int CustomerId,
    DateTime Date,
    string? Type,
    string? Description,
    decimal Debt,
    decimal Payment);

public record V5Backup(
    IReadOnlyList<V5Customer> Customers,
    IReadOnlyList<V5Transaction> Transactions);

/// <summary>
/// Parses a real "Veresiye 5" <c>.exa</c> backup. The file embeds a SQLite database
/// (<c>c:\terkon\veresiye\data\frm1.edb</c>) split into length-prefixed zlib chunks; this
/// reconstructs it, then reads the <c>CariKart</c> (customers) and <c>Data</c> (ledger) tables.
///
/// Text in that database is stored as raw <b>Windows-1254</b> (legacy Turkish) bytes even though
/// SQLite reports the database as UTF-8, so every text column is read as a BLOB and decoded with
/// code page 1254. Reading it as a string instead lets SQLite mis-decode the bytes as UTF-8 and
/// permanently replaces every Turkish letter (Ç, Ğ, İ, Ö, Ş, Ü) with "?".
/// </summary>
public class Veresiye5BackupReader
{
    private const string Prefix = @"c:\terkon\veresiye\data\";
    private static readonly Encoding Turkish;

    static Veresiye5BackupReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Turkish = Encoding.GetEncoding(1254);
    }

    public V5Backup Read(Stream input)
    {
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);

        var embeddedDbPath = Extract(buffer.ToArray(), Encoding.ASCII.GetBytes(Prefix + "frm1.edb"));
        try
        {
            return ReadDb(embeddedDbPath);
        }
        finally
        {
            try { File.Delete(embeddedDbPath); } catch { /* best effort */ }
        }
    }

    private static V5Backup ReadDb(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();

        var customers = new List<V5Customer>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT ID, CAST(Unvan AS BLOB), CAST(Gsm AS BLOB), CAST(Tel AS BLOB), CAST(CNot AS BLOB) FROM CariKart";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                customers.Add(new V5Customer(
                    reader.GetInt32(0),
                    Text(reader, 1) ?? "",
                    FirstNonEmpty(Text(reader, 2), Text(reader, 3)),
                    Text(reader, 4)));
            }
        }

        var transactions = new List<V5Transaction>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT ID, CAST(Tarih AS BLOB), CAST(Tur AS BLOB), CAST(Aciklama AS BLOB), Borc, Alacak, CariKartID " +
                "FROM Data ORDER BY ID";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DateTime.TryParse(Text(reader, 1), out var date);
                transactions.Add(new V5Transaction(
                    reader.GetInt32(0),
                    reader.GetInt32(6),
                    date,
                    Text(reader, 2),
                    Text(reader, 3),
                    reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                    reader.IsDBNull(5) ? 0m : reader.GetDecimal(5)));
            }
        }

        return new V5Backup(customers, transactions);
    }

    /// <summary>Reads a text column that was <c>CAST(... AS BLOB)</c> and decodes it from Windows-1254.</summary>
    private static string? Text(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var bytes = reader.GetFieldValue<byte[]>(ordinal);
        var value = Turkish.GetString(bytes).Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Reconstructs the embedded SQLite file from its zlib chunks and writes it to a temp path.</summary>
    private static string Extract(byte[] data, byte[] marker)
    {
        var start = IndexOf(data, marker, 0);
        if (start < 0)
            throw new InvalidOperationException("Veresiye 5 frm1.edb was not found in the backup.");

        var p = start + marker.Length + 8;
        using var output = new MemoryStream();
        while (p + 4 <= data.Length)
        {
            var chunkLength = BitConverter.ToInt32(data, p);
            if (chunkLength <= 0 || p + 4 + chunkLength > data.Length)
                break;

            try
            {
                using var chunk = new MemoryStream(data, p + 4, chunkLength);
                using var inflate = new ZLibStream(chunk, CompressionMode.Decompress);
                inflate.CopyTo(output);
            }
            catch
            {
                break;
            }

            p += 4 + chunkLength;
            if (p < data.Length &&
                IndexOf(data, Encoding.ASCII.GetBytes(@"c:\terkon\veresiye\data\"), p) == p)
                break;
        }

        var bytes = output.ToArray();
        if (bytes.Length < 16 || Encoding.ASCII.GetString(bytes, 0, 15) != "SQLite format 3")
            throw new InvalidOperationException("Could not reconstruct the embedded SQLite database.");

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }
}
