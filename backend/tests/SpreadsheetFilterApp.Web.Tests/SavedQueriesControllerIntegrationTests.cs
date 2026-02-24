using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SpreadsheetFilterApp.Web.Tests;

public sealed class SavedQueriesControllerIntegrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_GetById_Delete_AndList_WorksEndToEnd()
    {
        var unique = Guid.NewGuid().ToString("N");
        var created = await CreateSavedQueryAsync(
            $"Mesclar pagamentos e clientes ativos {unique}",
            "return rows.Where(r => r.status == \"Ativo\").ToList();");

        Assert.True(created.Id > 0);
        Assert.Equal($"Mesclar pagamentos e clientes ativos {unique}", created.Name);
        Assert.Equal("return rows.Where(r => r.status == \"Ativo\").ToList();", created.LinqCode);

        var byIdResponse = await _client.GetAsync($"/api/saved-queries/{created.Id}");
        byIdResponse.EnsureSuccessStatusCode();
        var byId = await byIdResponse.Content.ReadFromJsonAsync<SavedQueryDto>(JsonOptions);
        Assert.NotNull(byId);
        Assert.Equal(created.Id, byId.Id);
        Assert.Equal(created.LinqCode, byId.LinqCode);

        var listResponse = await _client.GetAsync("/api/saved-queries");
        listResponse.EnsureSuccessStatusCode();
        var list = await listResponse.Content.ReadFromJsonAsync<List<SavedQueryDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list, item => item.Id == created.Id && item.Name == created.Name);

        var deleteResponse = await _client.DeleteAsync($"/api/saved-queries/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDeleteById = await _client.GetAsync($"/api/saved-queries/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDeleteById.StatusCode);

        var listAfterDelete = await _client.GetFromJsonAsync<List<SavedQueryDto>>("/api/saved-queries", JsonOptions);
        Assert.NotNull(listAfterDelete);
        Assert.DoesNotContain(listAfterDelete, item => item.Id == created.Id);
    }

    private async Task<SavedQueryDto> CreateSavedQueryAsync(string name, string linqCode)
    {
        var response = await _client.PostAsJsonAsync("/api/saved-queries", new { name, linqCode });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<SavedQueryDto>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Create saved query returned empty payload.");
    }

    public sealed class SavedQueryDto
    {
        public long Id { get; init; }
        public required string Name { get; init; }
        public required string LinqCode { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
