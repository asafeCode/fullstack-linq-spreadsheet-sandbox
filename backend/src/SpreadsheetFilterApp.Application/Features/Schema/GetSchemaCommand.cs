namespace SpreadsheetFilterApp.Application.Features.Schema;

public sealed class GetSchemaCommand
{
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }
}
