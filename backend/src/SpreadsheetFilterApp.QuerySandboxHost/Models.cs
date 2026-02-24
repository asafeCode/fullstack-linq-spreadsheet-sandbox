using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json.Serialization;
using SpreadsheetFilterApp.QuerySandboxHost.Normalization;

namespace SpreadsheetFilterApp.QuerySandboxHost;

public sealed class SandboxRequest
{
    public required string Code { get; init; }
    public required SheetPayload Sheet1 { get; init; }
    public SheetPayload? Sheet2 { get; init; }
    public SheetPayload? Sheet3 { get; init; }
    public int MaxRows { get; init; } = 2000;
    public int HardLimitRows { get; init; } = 10000;
    public int TimeoutMs { get; init; } = 2000;
}

public sealed class SheetPayload
{
    public required List<string> Headers { get; init; }
    public required List<Dictionary<string, string?>> Rows { get; init; }
}

public sealed class SandboxResponse
{
    public bool Success { get; init; }
    public List<SandboxDiagnostic> Diagnostics { get; init; } = [];
    public List<Dictionary<string, object?>> Rows { get; init; } = [];
    public object? Aggregate { get; init; }
    public bool Truncated { get; init; }
    public long ElapsedMs { get; init; }
}

public sealed class SandboxDiagnostic
{
    public required string Message { get; init; }
    public required string Severity { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
}

public sealed class Sheet
{
    public required string Name { get; init; }
    public required IReadOnlyList<dynamic> Rows { get; init; }
}

public sealed class RowValue
{
    private readonly string? _value;

    public RowValue(string? value)
    {
        _value = value;
    }

    public string Normalize()
    {
        return NameComparisonNormalizer.Normalize(_value);
    }

    public string normalize()
    {
        return Normalize();
    }

    public string? Raw => _value;

    public override string ToString()
    {
        return _value ?? string.Empty;
    }

    public static implicit operator string?(RowValue value)
    {
        return value._value;
    }

    public static bool operator ==(RowValue? left, RowValue? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return string.Equals(left._value, right._value, StringComparison.Ordinal);
    }

    public static bool operator !=(RowValue? left, RowValue? right)
    {
        return !(left == right);
    }

    public static bool operator ==(RowValue? left, string? right)
    {
        return string.Equals(left?._value, right, StringComparison.Ordinal);
    }

    public static bool operator !=(RowValue? left, string? right)
    {
        return !(left == right);
    }

    public static bool operator ==(string? left, RowValue? right)
    {
        return string.Equals(left, right?._value, StringComparison.Ordinal);
    }

    public static bool operator !=(string? left, RowValue? right)
    {
        return !(left == right);
    }

    public override bool Equals(object? obj)
    {
        if (obj is RowValue other)
        {
            return this == other;
        }

        if (obj is string text)
        {
            return this == text;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return (_value ?? string.Empty).GetHashCode(StringComparison.Ordinal);
    }
}

public sealed class RowRef : DynamicObject
{
    private readonly IReadOnlyDictionary<string, string?> _values;

    public RowRef(IReadOnlyDictionary<string, string?> values)
    {
        _values = values;
    }

    public string? Str(string column)
    {
        return _values.TryGetValue(column, out var value) ? value : null;
    }

    public int Int(string column)
    {
        var raw = Str(column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var normalized = raw.Trim().TrimEnd('%');
        return int.TryParse(normalized, out var parsed) ? parsed : 0;
    }

    public double Dbl(string column)
    {
        var raw = Str(column);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var normalized = raw.Trim().TrimEnd('%').Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    public DateTime? Date(string column)
    {
        var raw = Str(column);
        return DateTime.TryParse(raw, out var parsed) ? parsed : null;
    }

    public string Norm(string column)
    {
        return NameComparisonNormalizer.Normalize(Str(column));
    }

    public bool EqualsNorm(string column, string? value)
    {
        return NameComparisonNormalizer.EqualsNormalized(Str(column), value);
    }

    public Dictionary<string, object?> ToDictionary()
    {
        return _values.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = new RowValue(Str(binder.Name));
        return true;
    }
}

public sealed class SandboxGlobals
{
    public required IEnumerable<dynamic> rows { get; init; }
    public required Sheet sheet1 { get; init; }
    public Sheet? sheet2 { get; init; }
    public Sheet? sheet3 { get; init; }
}
