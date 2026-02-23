namespace SpreadsheetFilterApp.Application.Common;

public static class Errors
{
    public const string FileNotFound = "File token not found.";
    public const string EmptyFile = "Spreadsheet has no rows.";
    public const string InvalidQuery = "LINQ query is invalid.";
    public const string SandboxViolation = "Query violates sandbox policy.";
}
