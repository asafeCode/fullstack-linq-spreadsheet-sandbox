using CsvHelper;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Domain.ValueObjects;
using System.Globalization;

namespace SpreadsheetFilterApp.Infrastructure.Spreadsheet.Csv;

public sealed class CsvSpreadsheetWriter : ISpreadsheetWriter
{
    public bool CanWrite(SpreadsheetFormat format) => format == SpreadsheetFormat.Csv;

    public async Task<byte[]> WriteAsync(QueryTable table, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var header in table.Headers)
        {
            csv.WriteField(header);
        }

        await csv.NextRecordAsync();

        foreach (var row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var header in table.Headers)
            {
                csv.WriteField(row.TryGetValue(header, out var value) ? value : null);
            }

            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(cancellationToken);
        return stream.ToArray();
    }
}
