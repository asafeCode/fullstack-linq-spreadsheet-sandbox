using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Infrastructure.Spreadsheet.Xlsx;

public sealed class XlsxSpreadsheetWriter : ISpreadsheetWriter
{
    public bool CanWrite(SpreadsheetFormat format) => format == SpreadsheetFormat.Xlsx;

    public Task<byte[]> WriteAsync(QueryTable table, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Result");

        for (var col = 0; col < table.Headers.Count; col++)
        {
            worksheet.Cell(1, col + 1).Value = table.Headers[col];
            worksheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = table.Rows[rowIndex];

            for (var col = 0; col < table.Headers.Count; col++)
            {
                var header = table.Headers[col];
                worksheet.Cell(rowIndex + 2, col + 1).Value = row.TryGetValue(header, out var value)
                    ? value?.ToString()
                    : null;
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }
}
