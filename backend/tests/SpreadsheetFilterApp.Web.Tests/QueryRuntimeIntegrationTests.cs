using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SpreadsheetFilterApp.Web.Tests;

public sealed class QueryRuntimeIntegrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Status_ReturnsStageAsString()
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Nome\nAna\n"));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file, "file1", "sample.csv");

        var uploadResponse = await _client.PostAsync("/api/query/upload", form);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        using var uploadDoc = JsonDocument.Parse(uploadJson);
        var jobId = uploadDoc.RootElement.GetProperty("jobId").GetString();

        Assert.False(string.IsNullOrWhiteSpace(jobId));

        var statusResponse = await _client.GetAsync($"/api/query/status/{jobId}");
        statusResponse.EnsureSuccessStatusCode();
        var statusJson = await statusResponse.Content.ReadAsStringAsync();
        using var statusDoc = JsonDocument.Parse(statusJson);

        var stage = statusDoc.RootElement.GetProperty("stage");
        Assert.Equal(JsonValueKind.String, stage.ValueKind);
    }

    [Fact]
    public async Task Contract_WithTwoSheets_ReturnsFieldsAndPreviewForBoth()
    {
        using var form = new MultipartFormDataContent();
        using var file1 = new ByteArrayContent(Encoding.UTF8.GetBytes("Nome,Status\nAna,Ativa\nBruno,Inativa\n"));
        file1.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file1, "file1", "sheet1.csv");

        using var file2 = new ByteArrayContent(Encoding.UTF8.GetBytes("Name,CS Responsavel\nAna,AnaCS\nBruno,\n"));
        file2.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file2, "file2", "sheet2.csv");

        var uploadResponse = await _client.PostAsync("/api/query/upload", form);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        using var uploadDoc = JsonDocument.Parse(uploadJson);
        var jobId = uploadDoc.RootElement.GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        var ready = false;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var statusResponse = await _client.GetAsync($"/api/query/status/{jobId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            using var statusDoc = JsonDocument.Parse(statusJson);
            var stage = statusDoc.RootElement.GetProperty("stage").GetString();
            if (string.Equals(stage, "Ready", StringComparison.OrdinalIgnoreCase))
            {
                ready = true;
                break;
            }
        }

        Assert.True(ready);

        var contractResponse = await _client.GetAsync($"/api/query/contract?jobId={jobId}");
        contractResponse.EnsureSuccessStatusCode();
        var contractJson = await contractResponse.Content.ReadAsStringAsync();
        using var contractDoc = JsonDocument.Parse(contractJson);
        var sheets = contractDoc.RootElement.GetProperty("sheets");

        Assert.Equal(2, sheets.GetArrayLength());

        var sheet1 = sheets.EnumerateArray().First(x => x.GetProperty("sheetName").GetString() == "sheet1");
        var sheet2 = sheets.EnumerateArray().First(x => x.GetProperty("sheetName").GetString() == "sheet2");

        Assert.True(sheet1.GetProperty("columns").GetArrayLength() > 0);
        Assert.True(sheet2.GetProperty("columns").GetArrayLength() > 0);
        Assert.True(sheet1.GetProperty("previewRows").GetArrayLength() > 0);
        Assert.True(sheet2.GetProperty("previewRows").GetArrayLength() > 0);
    }

    [Fact]
    public async Task PreviewEndpoint_ReturnsPagedRows()
    {
        using var form = new MultipartFormDataContent();
        using var file1 = new ByteArrayContent(Encoding.UTF8.GetBytes("Nome,Status\nAna,Ativa\nBruno,Inativa\nCarla,Ativa\n"));
        file1.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file1, "file1", "sheet1.csv");

        var uploadResponse = await _client.PostAsync("/api/query/upload", form);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        using var uploadDoc = JsonDocument.Parse(uploadJson);
        var jobId = uploadDoc.RootElement.GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var statusResponse = await _client.GetAsync($"/api/query/status/{jobId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            using var statusDoc = JsonDocument.Parse(statusJson);
            if (statusDoc.RootElement.GetProperty("stage").GetString() == "Ready")
            {
                break;
            }
        }

        var previewPageResponse = await _client.GetAsync($"/api/query/{jobId}/preview/sheet1?page=1&pageSize=2");
        previewPageResponse.EnsureSuccessStatusCode();
        var previewJson = await previewPageResponse.Content.ReadAsStringAsync();
        using var previewDoc = JsonDocument.Parse(previewJson);

        Assert.Equal(2, previewDoc.RootElement.GetProperty("rows").GetArrayLength());
        Assert.Equal(3, previewDoc.RootElement.GetProperty("totalRows").GetInt32());
    }

    [Fact]
    public async Task Unify_ThenRunWithoutProjection_UsesUnifiedDatasetAndReturnsCompleteRows()
    {
        using var form = new MultipartFormDataContent();
        using var file1 = new ByteArrayContent(Encoding.UTF8.GetBytes("nome,id\nAna,1\nBruno,2\n"));
        file1.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file1, "file1", "base.csv");

        using var file2 = new ByteArrayContent(Encoding.UTF8.GetBytes("nome_guardiao,telefone\nAna,111\nBruno,222\n"));
        file2.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file2, "file2", "guardiao.csv");

        var uploadResponse = await _client.PostAsync("/api/query/upload", form);
        uploadResponse.EnsureSuccessStatusCode();
        var uploadJson = await uploadResponse.Content.ReadAsStringAsync();
        using var uploadDoc = JsonDocument.Parse(uploadJson);
        var jobId = uploadDoc.RootElement.GetProperty("jobId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jobId));

        var ready = false;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var statusResponse = await _client.GetAsync($"/api/query/status/{jobId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            using var statusDoc = JsonDocument.Parse(statusJson);
            if (string.Equals(statusDoc.RootElement.GetProperty("stage").GetString(), "Ready", StringComparison.OrdinalIgnoreCase))
            {
                ready = true;
                break;
            }
        }

        Assert.True(ready);

        var unifyResponse = await _client.PostAsJsonAsync($"/api/query/{jobId}/unify", new
        {
            primarySheetName = "sheet1",
            primaryKeyColumn = "nome",
            comparisons = new[]
            {
                new
                {
                    sheetName = "sheet2",
                    compareColumn = "nome_guardiao"
                }
            }
        });
        unifyResponse.EnsureSuccessStatusCode();

        var executeResponse = await _client.PostAsJsonAsync($"/api/query/{jobId}/execute", new
        {
            code = "return rows.Where(r => true).ToList();",
            maxRows = 100,
            timeoutMs = 3000
        });
        executeResponse.EnsureSuccessStatusCode();

        var executeJson = await executeResponse.Content.ReadAsStringAsync();
        using var executeDoc = JsonDocument.Parse(executeJson);
        var queryId = executeDoc.RootElement.GetProperty("queryId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(queryId));

        JsonDocument? finalStatusDoc = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var statusResponse = await _client.GetAsync($"/api/query/execute/{queryId}");
            statusResponse.EnsureSuccessStatusCode();
            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            finalStatusDoc?.Dispose();
            finalStatusDoc = JsonDocument.Parse(statusJson);
            var stage = finalStatusDoc.RootElement.GetProperty("stage").GetString();
            if (stage is "Completed" or "Failed")
            {
                break;
            }
        }

        Assert.NotNull(finalStatusDoc);
        var root = finalStatusDoc!.RootElement;
        Assert.Equal("Completed", root.GetProperty("stage").GetString());
        Assert.True(root.GetProperty("success").GetBoolean());

        var rows = root.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());

        var first = rows[0];
        Assert.True(first.TryGetProperty("nome", out _));
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("sheet2__telefone", out _));
    }
}
