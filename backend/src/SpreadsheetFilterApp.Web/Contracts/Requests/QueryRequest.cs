namespace SpreadsheetFilterApp.Web.Contracts.Requests;

public sealed class QueryRequest
{
    public required string FileToken { get; init; }
    public required string LinqCode { get; init; }
    public required string OutputFormat { get; init; }
}
