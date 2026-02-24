using System.Collections.Generic;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Domain.Entities;

public sealed class Spreadsheet
{
    public required string FileName { get; init; }

    public required SpreadsheetFormat Format { get; init; }

    public required IReadOnlyCollection<ColumnSchema> Columns { get; init; }

    public required IReadOnlyCollection<IReadOnlyDictionary<string, string?>> Rows { get; init; }
}
