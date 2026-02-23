using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;

public interface ISpreadsheetReader
{
    bool CanRead(SpreadsheetFormat format);
    Task<SpreadsheetReadResult> ReadAsync(Stream stream, CancellationToken cancellationToken);
}

public sealed class SpreadsheetReadResult
{
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
}
