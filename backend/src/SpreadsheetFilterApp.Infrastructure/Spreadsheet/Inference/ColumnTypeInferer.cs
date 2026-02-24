using System;
using System.Collections.Generic;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using System.Globalization;
using System.Linq;

namespace SpreadsheetFilterApp.Infrastructure.Spreadsheet.Inference;

public sealed class ColumnTypeInferer : IColumnTypeInferer
{
    public IReadOnlyDictionary<string, string> Infer(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyList<string> headers,
        int sampleSize = 200)
    {
        var sample = rows.Take(sampleSize).ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var values = sample
                .Select(x => x.TryGetValue(header, out var value) ? value : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            result[header] = InferType(values);
        }

        return result;
    }

    private static string InferType(IReadOnlyList<string?> values)
    {
        if (values.Count == 0)
        {
            return "string";
        }

        if (values.All(x => bool.TryParse(x, out _)))
        {
            return "bool";
        }

        if (values.All(x => decimal.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture, out _)))
        {
            return "decimal";
        }

        if (values.All(x => DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
        {
            return "datetime";
        }

        return "string";
    }
}
