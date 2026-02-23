using CsvHelper;
using CsvHelper.Configuration;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Domain.ValueObjects;
using System.Globalization;
using System.Text;

namespace SpreadsheetFilterApp.Infrastructure.Spreadsheet.Csv;

public sealed class CsvSpreadsheetReader : ISpreadsheetReader
{
    public bool CanRead(SpreadsheetFormat format) => format == SpreadsheetFormat.Csv;

    public async Task<SpreadsheetReadResult> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var csvText = DecodeCsvBytes(buffer.ToArray());
        csvText = csvText.Replace("\0", string.Empty, StringComparison.Ordinal);

        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            DetectDelimiter = true
        });

        if (!await csv.ReadAsync())
        {
            return new SpreadsheetReadResult
            {
                Headers = [],
                Rows = []
            };
        }

        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? [])
            .Select(SanitizeHeader)
            .ToList();
        var rows = new List<IReadOnlyDictionary<string, string?>>();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new Dictionary<string, string?>(headers.Count, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                var value = csv.TryGetField(index, out string? rawValue) ? rawValue : null;
                row[headers[index]] = string.IsNullOrWhiteSpace(value) ? null : value;
            }

            rows.Add(row);
        }

        return new SpreadsheetReadResult
        {
            Headers = headers,
            Rows = rows
        };
    }

    private static string DecodeCsvBytes(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        if (HasUtf16LeBom(data))
        {
            return Encoding.Unicode.GetString(data, 2, data.Length - 2);
        }

        if (HasUtf16BeBom(data))
        {
            return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
        }

        if (LooksLikeUtf16Le(data))
        {
            return Encoding.Unicode.GetString(data);
        }

        if (LooksLikeUtf16Be(data))
        {
            return Encoding.BigEndianUnicode.GetString(data);
        }

        try
        {
            // Strict UTF-8 first (with BOM support). Most modern exports should hit this path.
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(data);
        }
        catch (DecoderFallbackException)
        {
            // Fallback for ANSI/legacy exports (common in spreadsheet tools on Windows).
            return Encoding.Latin1.GetString(data);
        }
    }

    private static bool HasUtf16LeBom(byte[] data)
    {
        return data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE;
    }

    private static bool HasUtf16BeBom(byte[] data)
    {
        return data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF;
    }

    private static bool LooksLikeUtf16Le(byte[] data)
    {
        var sampleLength = Math.Min(data.Length, 512);
        var oddZeroes = 0;
        var oddCount = 0;
        var evenZeroes = 0;
        var evenCount = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            if ((i & 1) == 0)
            {
                evenCount++;
                if (data[i] == 0)
                {
                    evenZeroes++;
                }
            }
            else
            {
                oddCount++;
                if (data[i] == 0)
                {
                    oddZeroes++;
                }
            }
        }

        return oddCount > 0
            && evenCount > 0
            && oddZeroes >= oddCount * 6 / 10
            && evenZeroes <= evenCount / 10;
    }

    private static bool LooksLikeUtf16Be(byte[] data)
    {
        var sampleLength = Math.Min(data.Length, 512);
        var oddZeroes = 0;
        var oddCount = 0;
        var evenZeroes = 0;
        var evenCount = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            if ((i & 1) == 0)
            {
                evenCount++;
                if (data[i] == 0)
                {
                    evenZeroes++;
                }
            }
            else
            {
                oddCount++;
                if (data[i] == 0)
                {
                    oddZeroes++;
                }
            }
        }

        return oddCount > 0
            && evenCount > 0
            && evenZeroes >= evenCount * 6 / 10
            && oddZeroes <= oddCount / 10;
    }

    private static string SanitizeHeader(string header)
    {
        return header.Trim().TrimStart('\uFEFF', '\0');
    }
}
