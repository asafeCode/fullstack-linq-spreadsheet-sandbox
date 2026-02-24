using System.Collections.Generic;

namespace SpreadsheetFilterApp.Domain.Services;

public interface IColumnNameNormalizer
{
    IReadOnlyList<NormalizedColumnResult> Normalize(IEnumerable<string> originalColumns);
}

public sealed record NormalizedColumnResult(string OriginalName, string NormalizedName);
