namespace SpreadsheetFilterApp.Application.DTOs;

public sealed class ColumnSchemaDto
{
    public required string OriginalName { get; init; }
    public required string NormalizedName { get; init; }
    public required string InferredType { get; init; }
}
