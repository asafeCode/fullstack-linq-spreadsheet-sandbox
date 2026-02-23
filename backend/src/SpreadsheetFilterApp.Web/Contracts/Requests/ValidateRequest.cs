namespace SpreadsheetFilterApp.Web.Contracts.Requests;

public sealed class ValidateRequest
{
    public required string FileToken { get; init; }
    public required string LinqCode { get; init; }
}
