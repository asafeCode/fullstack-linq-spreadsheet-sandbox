using System.Collections.Generic;

namespace SpreadsheetFilterApp.Application.DTOs;

public sealed class SpreadsheetPreviewDto
{
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
    public required int RowCountPreview { get; init; }
}
