using System.Globalization;
using System.Text;

namespace SpreadsheetFilterApp.QuerySandboxHost.Normalization;

internal static class NameComparisonNormalizer
{
    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    internal static bool EqualsNormalized(string? left, string? right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }
}
