namespace SpreadsheetFilterApp.Application.Features.Query;

public sealed class RunLinqQueryCommand
{
    public required string FileToken { get; init; }
    public required string LinqCode { get; init; }
    public required string OutputFormat { get; init; }
    public bool GenerateFile { get; init; } = true;
    public int PreviewRowLimit { get; init; } = 50;
}
