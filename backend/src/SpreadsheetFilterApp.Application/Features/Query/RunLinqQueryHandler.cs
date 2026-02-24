using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpreadsheetFilterApp.Application.Abstractions.Persistence;
using SpreadsheetFilterApp.Application.Abstractions.Scripting;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Application.DTOs;
using SpreadsheetFilterApp.Application.Mapping;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Application.Features.Query;

public sealed class RunLinqQueryHandler(
    ITempFileStore tempFileStore,
    IEnumerable<ISpreadsheetReader> readers,
    IEnumerable<ISpreadsheetWriter> writers,
    ILinqSandbox linqSandbox)
{
    private readonly ITempFileStore _tempFileStore = tempFileStore;
    private readonly IEnumerable<ISpreadsheetReader> _readers = readers;
    private readonly IEnumerable<ISpreadsheetWriter> _writers = writers;
    private readonly ILinqSandbox _linqSandbox = linqSandbox;

    public async Task<QueryResultDto> HandleAsync(RunLinqQueryCommand command, CancellationToken cancellationToken)
    {
        var stored = await _tempFileStore.GetAsync(command.FileToken, cancellationToken)
            ?? throw new InvalidOperationException("Invalid fileToken.");
        var schema = await _tempFileStore.GetSchemaAsync(command.FileToken, cancellationToken)
            ?? throw new InvalidOperationException("Schema not found for this fileToken.");

        var reader = _readers.FirstOrDefault(x => x.CanRead(stored.Format))
            ?? throw new InvalidOperationException($"Reader not found for format {stored.Format}.");
        await using var stream = File.OpenRead(stored.FilePath);
        var readResult = await reader.ReadAsync(stream, cancellationToken);

        var execution = await _linqSandbox.ExecuteAsync(
            schema.Columns,
            readResult.Rows,
            command.LinqCode,
            command.GenerateFile ? null : command.PreviewRowLimit,
            cancellationToken);
        var outputFormat = SchemaMapper.ToFormat(command.OutputFormat);
        byte[] content;
        if (command.GenerateFile)
        {
            var writer = _writers.FirstOrDefault(x => x.CanWrite(outputFormat))
                ?? throw new InvalidOperationException($"Writer not found for format {outputFormat}.");

            content = await writer.WriteAsync(new QueryTable
            {
                Headers = execution.Headers,
                Rows = execution.Rows
            }, cancellationToken);
        }
        else
        {
            content = [];
        }

        var previewRows = execution.Rows.Take(command.PreviewRowLimit).ToList();

        return new QueryResultDto
        {
            Content = content,
            ContentType = ResolveContentType(outputFormat),
            FileName = $"result.{ResolveExtension(outputFormat)}",
            PreviewRows = previewRows,
            RowCountPreview = previewRows.Count,
            ElapsedMs = execution.ElapsedMs
        };
    }

    private static string ResolveContentType(SpreadsheetFormat format)
    {
        return format switch
        {
            SpreadsheetFormat.Csv => "text/csv",
            SpreadsheetFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }

    private static string ResolveExtension(SpreadsheetFormat format)
    {
        return format switch
        {
            SpreadsheetFormat.Csv => "csv",
            SpreadsheetFormat.Xlsx => "xlsx",
            _ => "bin"
        };
    }
}
