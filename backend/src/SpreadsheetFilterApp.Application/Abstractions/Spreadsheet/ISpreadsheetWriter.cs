using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;

public interface ISpreadsheetWriter
{
    bool CanWrite(SpreadsheetFormat format);
    Task<byte[]> WriteAsync(QueryTable table, CancellationToken cancellationToken);
}

public sealed class QueryTable
{
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
}
