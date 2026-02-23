using SpreadsheetFilterApp.Domain.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpreadsheetFilterApp.Infrastructure.Normalization;

public sealed partial class ColumnNameNormalizer : IColumnNameNormalizer
{
    private static readonly HashSet<string> CSharpKeywords =
    [
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const","continue",
        "decimal","default","delegate","do","double","else","enum","event","explicit","extern","false","finally",
        "fixed","float","for","foreach","goto","if","implicit","in","int","interface","internal","is","lock","long",
        "namespace","new","null","object","operator","out","override","params","private","protected","public","readonly",
        "ref","return","sbyte","sealed","short","sizeof","stackalloc","static","string","struct","switch","this","throw",
        "true","try","typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while"
    ];

    public IReadOnlyList<NormalizedColumnResult> Normalize(IEnumerable<string> originalColumns)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<NormalizedColumnResult>();

        foreach (var original in originalColumns)
        {
            var normalized = NormalizeCore(original);
            if (CSharpKeywords.Contains(normalized))
            {
                normalized = $"{normalized}_col";
            }

            var candidate = normalized;
            var suffix = 2;
            while (!used.Add(candidate))
            {
                candidate = $"{normalized}_{suffix++}";
            }

            list.Add(new NormalizedColumnResult(original, candidate));
        }

        return list;
    }

    private static string NormalizeCore(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        var clean = MultiUnderscore().Replace(sb.ToString().Trim('_'), "_");
        clean = CamelCaseBoundary().Replace(clean, "$1_$2");

        if (string.IsNullOrWhiteSpace(clean))
        {
            clean = "_col";
        }

        if (!char.IsLetter(clean[0]) && clean[0] != '_')
        {
            clean = $"_{clean}";
        }

        return clean.ToLowerInvariant();
    }

    [GeneratedRegex("_+")]
    private static partial Regex MultiUnderscore();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelCaseBoundary();
}
