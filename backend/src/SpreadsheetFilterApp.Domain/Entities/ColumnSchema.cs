namespace SpreadsheetFilterApp.Domain.Entities;

public sealed class ColumnSchema
{
    public required string OriginalName { get; init; }

    public required string NormalizedName { get; init; }

    public required string InferredType { get; init; }
}
