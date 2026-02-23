namespace SpreadsheetFilterApp.Application.Features.Validate;

public sealed class ValidateLinqCommand
{
    public required string FileToken { get; init; }
    public required string LinqCode { get; init; }
}
