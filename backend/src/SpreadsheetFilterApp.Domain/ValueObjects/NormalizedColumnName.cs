using SpreadsheetFilterApp.Domain.Exceptions;

namespace SpreadsheetFilterApp.Domain.ValueObjects;

public sealed record NormalizedColumnName
{
    public string Value { get; }

    public NormalizedColumnName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Normalized column name cannot be empty.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
