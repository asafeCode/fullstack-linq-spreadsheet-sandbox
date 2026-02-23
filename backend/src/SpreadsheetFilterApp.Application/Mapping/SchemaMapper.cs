using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Application.Mapping;

public static class SchemaMapper
{
    public static SpreadsheetFormat ToFormat(string fileNameOrFormat)
    {
        var value = fileNameOrFormat.Trim().ToLowerInvariant();

        return value switch
        {
            var x when x.EndsWith(".csv") || x == "csv" => SpreadsheetFormat.Csv,
            var x when x.EndsWith(".xlsx") || x == "xlsx" => SpreadsheetFormat.Xlsx,
            _ => throw new InvalidOperationException("Unsupported spreadsheet format.")
        };
    }
}
