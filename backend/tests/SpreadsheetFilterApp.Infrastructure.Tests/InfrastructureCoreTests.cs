using SpreadsheetFilterApp.Infrastructure.Normalization;
using SpreadsheetFilterApp.Infrastructure.Spreadsheet.Csv;
using SpreadsheetFilterApp.Infrastructure.Spreadsheet.Inference;
using System.Text;

namespace SpreadsheetFilterApp.Infrastructure.Tests;

public sealed class InfrastructureCoreTests
{
    [Fact]
    public void ColumnNameNormalizer_NormalizesDeduplicatesAndHandlesKeywords()
    {
        var sut = new ColumnNameNormalizer();

        var result = sut.Normalize(["Nome Completo", "Nome-Completo", "class", "acao", "  "]);
        var names = result.Select(x => x.NormalizedName).ToList();

        Assert.Equal("nome_completo", names[0]);
        Assert.Equal("nome_completo_2", names[1]);
        Assert.Equal("class_col", names[2]);
        Assert.Equal("acao", names[3]);
        Assert.Equal("_col", names[4]);
    }

    [Fact]
    public void ColumnNameNormalizer_IgnoresControlCharsAndKeepsWordShape()
    {
        var sut = new ColumnNameNormalizer();
        var result = sut.Normalize(["S\0t\0a\0t\0u\0s matr\u00EDcula"]);

        Assert.Single(result);
        Assert.Equal("status_matricula", result[0].NormalizedName);
    }

    [Fact]
    public void ColumnTypeInferer_InfersBoolDecimalDatetimeAndString()
    {
        var sut = new ColumnTypeInferer();
        var headers = new[] { "Ativo", "Idade", "Nascimento", "Nome" };
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["Ativo"] = "true", ["Idade"] = "18", ["Nascimento"] = "2025-01-31", ["Nome"] = "Ana" },
            new Dictionary<string, string?> { ["Ativo"] = "false", ["Idade"] = "22.5", ["Nascimento"] = "2024-12-01", ["Nome"] = "Bruno" }
        };

        var result = sut.Infer(rows, headers);

        Assert.Equal("bool", result["Ativo"]);
        Assert.Equal("decimal", result["Idade"]);
        Assert.Equal("datetime", result["Nascimento"]);
        Assert.Equal("string", result["Nome"]);
    }

    [Fact]
    public async Task CsvSpreadsheetReader_ReadsLatin1HeadersWithAccents()
    {
        var sut = new CsvSpreadsheetReader();
        var csv = "Nome,Endere\u00E7o,Status matr\u00EDcula\nAna,Rua A,Ativa\n";
        var bytes = Encoding.Latin1.GetBytes(csv);
        await using var stream = new MemoryStream(bytes);

        var result = await sut.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(3, result.Headers.Count);
        Assert.Equal("Endere\u00E7o", result.Headers[1]);
        Assert.Equal("Status matr\u00EDcula", result.Headers[2]);
        Assert.Single(result.Rows);
        Assert.Equal("Ativa", result.Rows[0]["Status matr\u00EDcula"]);
    }

    [Fact]
    public async Task CsvSpreadsheetReader_ReadsUtf16LeWithoutBomAndSemicolonDelimiter()
    {
        var sut = new CsvSpreadsheetReader();
        var csv = "\"Nome:\";\"Status matr\u00EDcula:\";\"Progresso:\"\n\"Ana\";\"Ativa\";\"50%\"\n";
        var bytes = Encoding.Unicode.GetBytes(csv);
        await using var stream = new MemoryStream(bytes);

        var result = await sut.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(3, result.Headers.Count);
        Assert.Equal("Nome:", result.Headers[0]);
        Assert.Equal("Status matr\u00EDcula:", result.Headers[1]);
        Assert.Equal("Progresso:", result.Headers[2]);
        Assert.Single(result.Rows);
        Assert.Equal("Ana", result.Rows[0]["Nome:"]);
        Assert.Equal("Ativa", result.Rows[0]["Status matr\u00EDcula:"]);
        Assert.Equal("50%", result.Rows[0]["Progresso:"]);
    }

    [Fact]
    public async Task CsvSpreadsheetReader_ReadsUtf16LeWithBom()
    {
        var sut = new CsvSpreadsheetReader();
        var csv = "Nome;Cidade\nJoao;Vitoria\n";
        var payload = Encoding.Unicode.GetBytes(csv);
        var bytes = new byte[payload.Length + 2];
        bytes[0] = 0xFF;
        bytes[1] = 0xFE;
        Buffer.BlockCopy(payload, 0, bytes, 2, payload.Length);
        await using var stream = new MemoryStream(bytes);

        var result = await sut.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(["Nome", "Cidade"], result.Headers);
        Assert.Single(result.Rows);
        Assert.Equal("Joao", result.Rows[0]["Nome"]);
        Assert.Equal("Vitoria", result.Rows[0]["Cidade"]);
    }

    [Fact]
    public async Task CsvSpreadsheetReader_StripsNullCharsBeforeParsing()
    {
        var sut = new CsvSpreadsheetReader();
        var csv = "\"Nome:\";\"Status:\"\n\"Ana\";\"Ativa\"\n";
        var withNulls = string.Concat(csv.Select(c => $"{c}\0"));
        var bytes = Encoding.UTF8.GetBytes(withNulls);
        await using var stream = new MemoryStream(bytes);

        var result = await sut.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(["Nome:", "Status:"], result.Headers);
        Assert.Single(result.Rows);
        Assert.Equal("Ana", result.Rows[0]["Nome:"]);
        Assert.Equal("Ativa", result.Rows[0]["Status:"]);
    }

    [Fact]
    public async Task CsvSpreadsheetReader_ReadsFirstColumnWhenUtf8BomIsPresent()
    {
        var sut = new CsvSpreadsheetReader();
        var csv = "Nome,Status\nAna,Ativa\n";
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = utf8Bom.GetBytes(csv);
        await using var stream = new MemoryStream(bytes);

        var result = await sut.ReadAsync(stream, CancellationToken.None);

        Assert.Single(result.Rows);
        Assert.Equal("Ana", result.Rows[0]["Nome"]);
        Assert.Equal("Ativa", result.Rows[0]["Status"]);
    }
}
