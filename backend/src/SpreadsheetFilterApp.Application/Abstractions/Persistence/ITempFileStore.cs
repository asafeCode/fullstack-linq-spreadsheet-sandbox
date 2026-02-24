using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Application.DTOs;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Application.Abstractions.Persistence;

public interface ITempFileStore
{
    Task<string> SaveUploadAsync(string fileName, SpreadsheetFormat format, byte[] data, CancellationToken cancellationToken);
    Task<StoredSpreadsheet?> GetAsync(string fileToken, CancellationToken cancellationToken);
    Task SaveSchemaAsync(string fileToken, StoredSchema schema, CancellationToken cancellationToken);
    Task<StoredSchema?> GetSchemaAsync(string fileToken, CancellationToken cancellationToken);
}

public sealed class StoredSpreadsheet
{
    public required string FileToken { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required SpreadsheetFormat Format { get; init; }
}

public sealed class StoredSchema
{
    public required IReadOnlyList<ColumnSchemaDto> Columns { get; init; }
}
