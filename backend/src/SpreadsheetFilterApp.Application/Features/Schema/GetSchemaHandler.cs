using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Application.Abstractions.Persistence;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Application.DTOs;
using SpreadsheetFilterApp.Application.Mapping;
using SpreadsheetFilterApp.Domain.Services;

namespace SpreadsheetFilterApp.Application.Features.Schema;

public sealed class GetSchemaHandler(
    IEnumerable<ISpreadsheetReader> readers,
    IColumnNameNormalizer columnNameNormalizer,
    IColumnTypeInferer typeInferer,
    ITempFileStore tempFileStore)
{
    private readonly IEnumerable<ISpreadsheetReader> _readers = readers;
    private readonly IColumnNameNormalizer _columnNameNormalizer = columnNameNormalizer;
    private readonly IColumnTypeInferer _typeInferer = typeInferer;
    private readonly ITempFileStore _tempFileStore = tempFileStore;

    public async Task<SpreadsheetSchemaDto> HandleAsync(GetSchemaCommand command, CancellationToken cancellationToken)
    {
        var format = SchemaMapper.ToFormat(command.FileName);
        var reader = _readers.FirstOrDefault(x => x.CanRead(format))
            ?? throw new InvalidOperationException($"Reader not found for format {format}.");

        await using var stream = new MemoryStream(command.Content);
        var parsed = await reader.ReadAsync(stream, cancellationToken);

        var normalized = _columnNameNormalizer.Normalize(parsed.Headers);
        var inferredTypes = _typeInferer.Infer(parsed.Rows, parsed.Headers);

        var columns = normalized.Select(item => new ColumnSchemaDto
        {
            OriginalName = item.OriginalName,
            NormalizedName = item.NormalizedName,
            InferredType = inferredTypes.TryGetValue(item.OriginalName, out var type) ? type : "string"
        }).ToList();

        var normalizedRows = parsed.Rows
            .Select(row => (IReadOnlyDictionary<string, string?>)columns.ToDictionary(
                column => column.NormalizedName,
                column => row.TryGetValue(column.OriginalName, out var value) ? value : null))
            .ToList();

        var fileToken = await _tempFileStore.SaveUploadAsync(command.FileName, format, command.Content, cancellationToken);
        await _tempFileStore.SaveSchemaAsync(fileToken, new StoredSchema
        {
            Columns = columns
        }, cancellationToken);

        var previewRows = normalizedRows.Take(50).ToList();

        return new SpreadsheetSchemaDto
        {
            FileToken = fileToken,
            Columns = columns,
            Preview = new SpreadsheetPreviewDto
            {
                Rows = previewRows,
                RowCountPreview = previewRows.Count
            }
        };
    }
}
