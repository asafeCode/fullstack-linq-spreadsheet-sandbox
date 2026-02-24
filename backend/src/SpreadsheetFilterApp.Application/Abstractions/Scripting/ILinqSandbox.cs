using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Application.Abstractions.Scripting;

public interface ILinqSandbox
{
    Task<LinqExecutionResult> ExecuteAsync(
        IReadOnlyList<ColumnSchemaDto> schema,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        string linqCode,
        int? maxResultRows,
        CancellationToken cancellationToken);
}

public sealed class LinqExecutionResult
{
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required long ElapsedMs { get; init; }
}
