using ClosedXML.Excel;
using FakeVeresiye.Api.Dtos;

namespace FakeVeresiye.Api.Services.Statements;

/// <summary>Renders a <see cref="StatementResponse"/> as a single-sheet, Turkish <c>.xlsx</c> workbook.</summary>
public class StatementExcelExporter
{
    private const string Money = "#,##0.00 \"₺\"";
    private const string Date = "dd.MM.yyyy";

    private static readonly XLColor DebtColor = XLColor.FromHtml("#B42318");
    private static readonly XLColor PaymentColor = XLColor.FromHtml("#067647");

    private static string TypeLabel(string type) => type == "Debt" ? "Borç" : "Tahsilat";

    public byte[] Build(StatementResponse s)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Ekstre");

        sheet.Cell("A1").Value = "Hesap Ekstresi";
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 14;

        sheet.Cell("A2").Value = "Müşteri";
        sheet.Cell("B2").Value = s.CustomerName;
        sheet.Cell("A3").Value = "Telefon";
        sheet.Cell("B3").Value = s.CustomerPhone ?? "";
        sheet.Cell("A4").Value = "Dönem";
        sheet.Cell("B4").Value = $"{s.From:dd.MM.yyyy} – {s.To:dd.MM.yyyy}";
        sheet.Cell("A5").Value = "Devir Bakiye";
        sheet.Cell("B5").Value = s.OpeningBalance;
        sheet.Cell("B5").Style.NumberFormat.Format = Money;

        const int header = 7;
        string[] titles = ["Tarih", "Tür", "Açıklama", "Borç", "Tahsilat", "Bakiye"];
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = sheet.Cell(header, i + 1);
            cell.Value = titles[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = header + 1;
        foreach (var line in s.Lines)
        {
            var isDebt = line.Type == "Debt";
            var color = isDebt ? DebtColor : PaymentColor;

            sheet.Cell(row, 1).Value = line.Date;
            sheet.Cell(row, 1).Style.NumberFormat.Format = Date;

            sheet.Cell(row, 2).Value = TypeLabel(line.Type);
            sheet.Cell(row, 2).Style.Font.FontColor = color;

            sheet.Cell(row, 3).Value = line.Description ?? "";
            sheet.Cell(row, 4).Value = line.Debt;
            sheet.Cell(row, 5).Value = line.Payment;
            sheet.Cell(row, 6).Value = line.RunningBalance;
            sheet.Range(row, 4, row, 6).Style.NumberFormat.Format = Money;
            sheet.Cell(row, isDebt ? 4 : 5).Style.Font.FontColor = color;

            row++;
        }

        sheet.Cell(row, 3).Value = "Toplam";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        sheet.Cell(row, 4).Value = s.TotalDebt;
        sheet.Cell(row, 4).Style.Font.FontColor = DebtColor;
        sheet.Cell(row, 5).Value = s.TotalPayment;
        sheet.Cell(row, 5).Style.Font.FontColor = PaymentColor;
        sheet.Cell(row, 6).Value = s.ClosingBalance;
        sheet.Range(row, 4, row, 6).Style.NumberFormat.Format = Money;
        sheet.Range(row, 3, row, 6).Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
