namespace SpreadsheetFilterApp.Application.DTOs;

public sealed class QueryResultDto
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> PreviewRows { get; init; }
    public required int RowCountPreview { get; init; }
    public required long ElapsedMs { get; init; }
}
