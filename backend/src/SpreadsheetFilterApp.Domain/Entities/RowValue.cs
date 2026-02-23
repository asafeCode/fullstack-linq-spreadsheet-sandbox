namespace SpreadsheetFilterApp.Domain.Entities;

public sealed class RowValue
{
    public required string ColumnName { get; init; }

    public string? Value { get; init; }
}
