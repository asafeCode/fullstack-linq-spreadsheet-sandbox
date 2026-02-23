namespace SpreadsheetFilterApp.Web.Contracts.Responses;

public sealed class SchemaResponse
{
    public required string FileToken { get; init; }
    public required IReadOnlyList<ColumnResponse> Columns { get; init; }
    public required PreviewResponse Preview { get; init; }
}

public sealed class ColumnResponse
{
    public required string OriginalName { get; init; }
    public required string NormalizedName { get; init; }
    public required string InferredType { get; init; }
}

public sealed class PreviewResponse
{
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
    public required int RowCountPreview { get; init; }
}
