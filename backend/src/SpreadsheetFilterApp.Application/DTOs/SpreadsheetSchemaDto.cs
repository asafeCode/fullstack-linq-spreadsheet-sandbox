namespace SpreadsheetFilterApp.Application.DTOs;

public sealed class SpreadsheetSchemaDto
{
    public required string FileToken { get; init; }
    public required IReadOnlyList<ColumnSchemaDto> Columns { get; init; }
    public required SpreadsheetPreviewDto Preview { get; init; }
}
