using FakeVeresiye.Api.Dtos;
using FakeVeresiye.Api.Services.Statements;
using QuestPDF.Infrastructure;

namespace FakeVeresiye.Api.Tests;

public class StatementExportTests
{
    static StatementExportTests() => QuestPDF.Settings.License = LicenseType.Community;


    private static StatementResponse Sample() => new(
        CustomerId: 1,
        CustomerName: "AYŞE ÇİÇEK",
        CustomerPhone: "555",
        From: new DateTime(2024, 1, 1),
        To: new DateTime(2024, 12, 31),
        OpeningBalance: 100m,
        Lines:
        [
            new StatementLine(1, new DateTime(2024, 2, 1), "Debt", "malzeme", 250m, 0m, 350m),
            new StatementLine(2, new DateTime(2024, 3, 1), "Payment", "nakit", 0m, 150m, 200m),
        ],
        TotalDebt: 250m,
        TotalPayment: 150m,
        ClosingBalance: 200m);

    [Fact]
    public void Excel_export_produces_a_workbook()
    {
        var bytes = new StatementExcelExporter().Build(Sample());
        Assert.NotEmpty(bytes);
        // .xlsx is a zip archive: "PK" magic bytes.
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public void Pdf_export_produces_a_document()
    {
        var bytes = new StatementPdfExporter().Build(Sample());
        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
