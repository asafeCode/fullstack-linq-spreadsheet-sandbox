namespace SpreadsheetFilterApp.Web.Contracts.Responses;

public sealed class ValidateResponse
{
    public required IReadOnlyList<DiagnosticResponse> Diagnostics { get; init; }
}

public sealed class DiagnosticResponse
{
    public required string Message { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string Severity { get; init; }
}
