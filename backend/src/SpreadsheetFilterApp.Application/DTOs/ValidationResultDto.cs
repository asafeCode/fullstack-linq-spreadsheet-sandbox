namespace SpreadsheetFilterApp.Application.DTOs;

public sealed class ValidationResultDto
{
    public required IReadOnlyList<ValidationDiagnosticDto> Diagnostics { get; init; }

    public static ValidationResultDto Empty() => new()
    {
        Diagnostics = []
    };
}

public sealed class ValidationDiagnosticDto
{
    public required string Message { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
    public required string Severity { get; init; }
}
