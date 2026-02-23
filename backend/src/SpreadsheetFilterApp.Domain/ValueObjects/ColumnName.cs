using SpreadsheetFilterApp.Domain.Exceptions;

namespace SpreadsheetFilterApp.Domain.ValueObjects;

public sealed record ColumnName
{
    public string Value { get; }

    public ColumnName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Column original name cannot be empty.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
