using ClosedXML.Excel;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Infrastructure.Spreadsheet.Xlsx;

public sealed class XlsxSpreadsheetReader : ISpreadsheetReader
{
    public bool CanRead(SpreadsheetFormat format) => format == SpreadsheetFormat.Xlsx;

    public Task<SpreadsheetReadResult> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var range = worksheet.RangeUsed();

        if (range is null)
        {
            return Task.FromResult(new SpreadsheetReadResult
            {
                Headers = [],
                Rows = []
            });
        }

        var firstRow = range.FirstRowUsed();
        var headers = firstRow.Cells().Select(x => x.GetString()).ToList();
        var rows = new List<IReadOnlyDictionary<string, string?>>();

        foreach (var dataRow in range.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < headers.Count; index++)
            {
                var cell = dataRow.Cell(index + 1);
                row[headers[index]] = cell.IsEmpty() ? null : cell.GetValue<string>();
            }

            rows.Add(row);
        }

        return Task.FromResult(new SpreadsheetReadResult
        {
            Headers = headers,
            Rows = rows
        });
    }
}
