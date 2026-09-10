using System.Globalization;
using FakeVeresiye.Api.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FakeVeresiye.Api.Services.Statements;

/// <summary>Renders a <see cref="StatementResponse"/> as an A4 PDF (Turkish) using QuestPDF.</summary>
public class StatementPdfExporter
{
    private static readonly CultureInfo Tr = new("tr-TR");

    private const string DebtColor = "#B42318";
    private const string PaymentColor = "#067647";

    private static string Money(decimal value) => value.ToString("N2", Tr) + " ₺";

    private static string TypeLabel(string type) => type == "Debt" ? "Borç" : "Tahsilat";

    public byte[] Build(StatementResponse s)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));

                page.Header().Column(header =>
                {
                    header.Item().Text("Hesap Ekstresi").FontSize(16).Bold();
                    header.Item().PaddingTop(4).Text($"Müşteri: {s.CustomerName}");
                    if (!string.IsNullOrWhiteSpace(s.CustomerPhone))
                        header.Item().Text($"Telefon: {s.CustomerPhone}");
                    header.Item().Text($"Dönem: {s.From:dd.MM.yyyy} – {s.To:dd.MM.yyyy}");
                    header.Item().Text($"Devir Bakiye: {Money(s.OpeningBalance)}");
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(70);
                        columns.RelativeColumn();
                        columns.ConstantColumn(72);
                        columns.ConstantColumn(72);
                        columns.ConstantColumn(82);
                    });

                    table.Header(h =>
                    {
                        HeadCell(h.Cell(), "Tarih");
                        HeadCell(h.Cell(), "Tür");
                        HeadCell(h.Cell(), "Açıklama");
                        HeadCell(h.Cell(), "Borç", right: true);
                        HeadCell(h.Cell(), "Tahsilat", right: true);
                        HeadCell(h.Cell(), "Bakiye", right: true);
                    });

                    foreach (var line in s.Lines)
                    {
                        var isDebt = line.Type == "Debt";
                        var typeColor = isDebt ? DebtColor : PaymentColor;

                        BodyCell(table.Cell(), $"{line.Date:dd.MM.yyyy}");
                        BodyCell(table.Cell(), TypeLabel(line.Type), color: typeColor);
                        BodyCell(table.Cell(), line.Description ?? "");
                        BodyCell(table.Cell(), line.Debt == 0 ? "" : Money(line.Debt), right: true,
                            color: line.Debt == 0 ? null : DebtColor);
                        BodyCell(table.Cell(), line.Payment == 0 ? "" : Money(line.Payment), right: true,
                            color: line.Payment == 0 ? null : PaymentColor);
                        BodyCell(table.Cell(), Money(line.RunningBalance), right: true);
                    }

                    table.Cell().ColumnSpan(3).BorderTop(1).PaddingVertical(3).Text("Toplam").Bold();
                    TotalCell(table.Cell(), Money(s.TotalDebt), DebtColor);
                    TotalCell(table.Cell(), Money(s.TotalPayment), PaymentColor);
                    TotalCell(table.Cell(), Money(s.ClosingBalance));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Sayfa ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void HeadCell(IContainer cell, string text, bool right = false)
    {
        cell = cell.Background(Colors.Grey.Lighten3).Padding(3);
        if (right) cell = cell.AlignRight();
        cell.Text(text).Bold();
    }

    private static void BodyCell(IContainer cell, string text, bool right = false, string? color = null)
    {
        cell = cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
        if (right) cell = cell.AlignRight();
        var span = cell.Text(text);
        if (color is not null) span.FontColor(color);
    }

    private static void TotalCell(IContainer cell, string text, string? color = null)
    {
        var span = cell.BorderTop(1).PaddingVertical(3).AlignRight().Text(text).Bold();
        if (color is not null) span.FontColor(color);
    }
}
