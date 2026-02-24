using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SpreadsheetFilterApp.Web.Tests;

public sealed class SpreadsheetsControllerIntegrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Schema_WithCsvUpload_ReturnsTokenColumnsAndPreview()
    {
        var schema = await UploadSchemaAsync();

        Assert.False(string.IsNullOrWhiteSpace(schema.FileToken));
        Assert.Equal(4, schema.Columns.Count);
        Assert.Equal(3, schema.Preview.RowCountPreview);
        Assert.Contains(schema.Columns, c => c.NormalizedName == "idade" && c.InferredType == "decimal");
    }

    [Fact]
    public async Task Validate_WithValidLinq_ReturnsNoDiagnostics()
    {
        var schema = await UploadSchemaAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/validate", new
        {
            fileToken = schema.FileToken,
            linqCode = "rows.Where(row => row.idade >= 18)"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Empty(payload.Diagnostics);
    }

    [Fact]
    public async Task Validate_WithForbiddenApi_ReturnsBadRequest()
    {
        var schema = await UploadSchemaAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/validate", new
        {
            fileToken = schema.FileToken,
            linqCode = "rows.Where(row => System.IO.File.Exists(\"c:/tmp/x\"))"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("Forbidden API in sandbox", problem);
    }

    [Fact]
    public async Task QueryPreview_WithFilter_ReturnsExpectedRows()
    {
        var schema = await UploadSchemaAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/query/preview", new
        {
            fileToken = schema.FileToken,
            linqCode = "rows.Where(row => row.status == \"Ativo\").Select(row => new { row.nome, row.idade })",
            outputFormat = "csv"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueryPreviewResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload.RowCountPreview);
        Assert.Equal(2, payload.Rows.Count);
        Assert.Equal("Bruno", payload.Rows[0]["nome"]);
    }

    [Fact]
    public async Task Query_WithCsvOutput_ReturnsDownloadWithCsvContentType()
    {
        var schema = await UploadSchemaAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/query", new
        {
            fileToken = schema.FileToken,
            linqCode = "rows.Where(row => row.idade >= 18)",
            outputFormat = "csv"
        });

        response.EnsureSuccessStatusCode();

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Equal("result.csv", response.Content.Headers.ContentDisposition.FileName?.Trim('"'));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task Validate_WithBlockLambdaAndTryParse_ReturnsNoDiagnostics()
    {
        var schema = await UploadSchemaForGroupingAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/validate", new
        {
            fileToken = schema.FileToken,
            linqCode = """
                rows
                    .Where(r => r.status_matricula == "Ativa")
                    .Where(r =>
                    {
                        var s = (r.progresso ?? "").Trim().TrimEnd('%');
                        return int.TryParse(s, out var p) && p > 10;
                    })
                    .GroupBy(r => r.progresso)
                    .Select(g => new { progresso = g.Key, total = g.Count() })
                """
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ValidateResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Empty(payload.Diagnostics);
    }

    [Fact]
    public async Task QueryPreview_WithProjectionAfterFilter_ReturnsProjectedColumns()
    {
        var schema = await UploadSchemaForGroupingAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/query/preview", new
        {
            fileToken = schema.FileToken,
            linqCode = "rows.Where(r => r.status_matricula == \"Ativa\").Select(r => new { r.nome, r.progresso })",
            outputFormat = "csv"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueryPreviewResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(4, payload.RowCountPreview);
        Assert.Equal(4, payload.Rows.Count);
        Assert.True(payload.Rows.All(x => x.ContainsKey("nome")));
        Assert.True(payload.Rows.All(x => x.ContainsKey("progresso")));
    }

    [Fact]
    public async Task QueryPreview_GroupByStatusMatricula_ReturnsAggregatedCounts()
    {
        var schema = await UploadSchemaForGroupingAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/query/preview", new
        {
            fileToken = schema.FileToken,
            linqCode = "rows.GroupBy(r => r.status_matricula).Select(g => new { status_matricula = g.Key, total = g.Count() })",
            outputFormat = "csv"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueryPreviewResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload.RowCountPreview);
        Assert.Contains(payload.Rows, x => x["status_matricula"] == "Ativa" && x["total"] == "4");
        Assert.Contains(payload.Rows, x => x["status_matricula"] == "Inativa" && x["total"] == "2");
    }

    [Fact]
    public async Task QueryPreview_GroupByProgressWithTryParseFilter_ReturnsExpectedBuckets()
    {
        var schema = await UploadSchemaForGroupingAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/query/preview", new
        {
            fileToken = schema.FileToken,
            linqCode = """
                rows
                    .Where(r => r.status_matricula == "Ativa")
                    .Where(r =>
                    {
                        var s = (r.progresso ?? "").Trim().TrimEnd('%');
                        return int.TryParse(s, out var p) && p > 10;
                    })
                    .GroupBy(r => r.progresso)
                    .Select(g => new
                    {
                        progresso = g.Key,
                        total = g.Count()
                    })
                """,
            outputFormat = "csv"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueryPreviewResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload.RowCountPreview);
        Assert.Contains(payload.Rows, x => x["progresso"] == "22%" && x["total"] == "1");
        Assert.Contains(payload.Rows, x => x["progresso"] == "50%" && x["total"] == "1");
    }

    [Fact]
    public async Task QueryPreview_WithConditionalQueryBuilderPattern_ReturnsProjectedRows()
    {
        var schema = await UploadSchemaForGroupingAsync();

        var response = await _client.PostAsJsonAsync("/api/spreadsheets/query/preview", new
        {
            fileToken = schema.FileToken,
            linqCode = """
                var status = "Ativa";
                string? nome = null;
                int? progressoMin = 10;
                int? progressoMax = null;

                var query = rows;

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(r => r.status_matricula == status);
                }

                if (!string.IsNullOrEmpty(nome))
                {
                    query = query.Where(r => (r.nome ?? "").Contains(nome));
                }

                if (progressoMin.HasValue)
                {
                    query = query.Where(r =>
                    {
                        var s = (r.progresso ?? "").Trim().TrimEnd('%');
                        return int.TryParse(s, out var p) && p >= progressoMin.Value;
                    });
                }

                if (progressoMax.HasValue)
                {
                    query = query.Where(r =>
                    {
                        var s = (r.progresso ?? "").Trim().TrimEnd('%');
                        return int.TryParse(s, out var p) && p <= progressoMax.Value;
                    });
                }

                query.Select(r => new
                {
                    r.nome,
                    r.status_matricula,
                    Progresso = int.TryParse((r.progresso ?? "").Trim().TrimEnd('%'), out var p) ? p : 0
                })
                """,
            outputFormat = "csv"
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<QueryPreviewResponseDto>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload.RowCountPreview);
        Assert.Contains(payload.Rows, x => x["nome"] == "Bruno" && x["Progresso"] == "22");
        Assert.Contains(payload.Rows, x => x["nome"] == "Carla" && x["Progresso"] == "50");
    }

    private async Task<SchemaResponseDto> UploadSchemaAsync()
    {
        const string csv = "Nome,Idade,Status,Pontos\nAna,17,Inativo,10\nBruno,22,Ativo,15\nCarla,34,Ativo,19\n";
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file, "file", "sample.csv");

        var response = await _client.PostAsync("/api/spreadsheets/schema", form);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SchemaResponseDto>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Schema response body was empty.");
    }

    private async Task<SchemaResponseDto> UploadSchemaForGroupingAsync()
    {
        const string csv =
            "Nome,Status matrícula,Progresso,Curso\n" +
            "Ana,Ativa,0%,LINQ Base\n" +
            "Bruno,Ativa,22%,LINQ Base\n" +
            "Carla,Ativa,50%,LINQ Avancado\n" +
            "Daniela,Inativa,90%,LINQ Avancado\n" +
            "Erica,Ativa,,LINQ Base\n" +
            "Fabio,Inativa,8%,LINQ Base\n";

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file, "file", "grouping-sample.csv");

        var response = await _client.PostAsync("/api/spreadsheets/schema", form);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SchemaResponseDto>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Schema response body was empty.");
    }

    public sealed class SchemaResponseDto
    {
        public required string FileToken { get; init; }
        public required List<ColumnDto> Columns { get; init; }
        public required PreviewDto Preview { get; init; }
    }

    public sealed class ColumnDto
    {
        public required string OriginalName { get; init; }
        public required string NormalizedName { get; init; }
        public required string InferredType { get; init; }
    }

    public sealed class PreviewDto
    {
        public required List<Dictionary<string, string>> Rows { get; init; }
        public int RowCountPreview { get; init; }
    }

    public sealed class ValidateResponseDto
    {
        public required List<DiagnosticDto> Diagnostics { get; init; }
    }

    public sealed class DiagnosticDto
    {
        public required string Message { get; init; }
        public required string Severity { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
    }

    public sealed class QueryPreviewResponseDto
    {
        public required List<Dictionary<string, string>> Rows { get; init; }
        public int RowCountPreview { get; init; }
        public long ElapsedMs { get; init; }
    }
}
