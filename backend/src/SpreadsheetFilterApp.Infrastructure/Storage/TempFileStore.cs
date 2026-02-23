using SpreadsheetFilterApp.Application.Abstractions.Persistence;
using SpreadsheetFilterApp.Domain.ValueObjects;
using System.Text.Json;

namespace SpreadsheetFilterApp.Infrastructure.Storage;

public sealed class TempFileStore : ITempFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _root;

    public TempFileStore()
    {
        _root = Path.Combine(Path.GetTempPath(), "SpreadsheetFilterApp");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveUploadAsync(string fileName, SpreadsheetFormat format, byte[] data, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(_root, token);
        Directory.CreateDirectory(dir);

        var extension = format == SpreadsheetFormat.Csv ? ".csv" : ".xlsx";
        var filePath = Path.Combine(dir, $"upload{extension}");

        await File.WriteAllBytesAsync(filePath, data, cancellationToken);

        var meta = new StoredSpreadsheet
        {
            FileToken = token,
            FilePath = filePath,
            FileName = fileName,
            Format = format
        };

        await File.WriteAllTextAsync(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(meta, JsonOptions), cancellationToken);
        return token;
    }

    public async Task<StoredSpreadsheet?> GetAsync(string fileToken, CancellationToken cancellationToken)
    {
        var metaPath = Path.Combine(_root, fileToken, "meta.json");
        if (!File.Exists(metaPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metaPath, cancellationToken);
        return JsonSerializer.Deserialize<StoredSpreadsheet>(json, JsonOptions);
    }

    public async Task SaveSchemaAsync(string fileToken, StoredSchema schema, CancellationToken cancellationToken)
    {
        var schemaPath = Path.Combine(_root, fileToken, "schema.json");
        await File.WriteAllTextAsync(schemaPath, JsonSerializer.Serialize(schema, JsonOptions), cancellationToken);
    }

    public async Task<StoredSchema?> GetSchemaAsync(string fileToken, CancellationToken cancellationToken)
    {
        var schemaPath = Path.Combine(_root, fileToken, "schema.json");
        if (!File.Exists(schemaPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        return JsonSerializer.Deserialize<StoredSchema>(json, JsonOptions);
    }
}
