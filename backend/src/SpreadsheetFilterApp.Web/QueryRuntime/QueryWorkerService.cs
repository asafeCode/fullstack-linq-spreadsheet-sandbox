using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;
using SpreadsheetFilterApp.Domain.Services;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

public sealed class QueryWorkerService : BackgroundService
{
    private readonly IQueryWorkQueue _queue;
    private readonly IQueryJobService _jobs;
    private readonly IEnumerable<ISpreadsheetReader> _readers;
    private readonly IColumnNameNormalizer _normalizer;
    private readonly IQuerySandboxProcessClient _sandbox;
    private readonly IHubContext<QueryProgressHub> _hub;
    private readonly QueryRuntimeOptions _options;
    private readonly SemaphoreSlim _semaphore;

    public QueryWorkerService(
        IQueryWorkQueue queue,
        IQueryJobService jobs,
        IEnumerable<ISpreadsheetReader> readers,
        IColumnNameNormalizer normalizer,
        IQuerySandboxProcessClient sandbox,
        IHubContext<QueryProgressHub> hub,
        IOptions<QueryRuntimeOptions> options)
    {
        _queue = queue;
        _jobs = jobs;
        _readers = readers;
        _normalizer = normalizer;
        _sandbox = sandbox;
        _hub = hub;
        _options = options.Value;
        _semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentJobs), Math.Max(1, _options.MaxConcurrentJobs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var item = await _queue.DequeueAsync(stoppingToken);
            await _semaphore.WaitAsync(stoppingToken);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (item.Kind == QueryWorkKind.ParseUpload)
                    {
                        await ParseUploadAsync(item.JobId, stoppingToken);
                    }
                    else if (item.Kind == QueryWorkKind.ExecuteQuery && item.QueryId is not null && item.Query is not null)
                    {
                        await ExecuteQueryAsync(item.JobId, item.QueryId, item.Query, stoppingToken);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }, stoppingToken);
        }
    }

    private async Task ParseUploadAsync(string jobId, CancellationToken cancellationToken)
    {
        UpdateJob(jobId, RuntimeStage.Parsing, 10, "Parsing uploaded files...");

        try
        {
            var dir = _jobs.GetJobDirectory(jobId);
            var sheet1Path = Directory.EnumerateFiles(dir, "sheet1.*").FirstOrDefault()
                ?? throw new InvalidOperationException("sheet1 file not found.");
            var sheet2Path = Directory.EnumerateFiles(dir, "sheet2.*").FirstOrDefault();
            var sheet3Path = Directory.EnumerateFiles(dir, "sheet3.*").FirstOrDefault();

            var sheet1 = await ReadSheetAsync(jobId, "sheet1", sheet1Path, cancellationToken);
            UpdateJob(jobId, RuntimeStage.Parsing, 50, "First sheet parsed.");

            StoredSheet? sheet2 = null;
            if (!string.IsNullOrWhiteSpace(sheet2Path))
            {
                sheet2 = await ReadSheetAsync(jobId, "sheet2", sheet2Path, cancellationToken);
                UpdateJob(jobId, RuntimeStage.Parsing, 75, "Second sheet parsed.");
            }

            StoredSheet? sheet3 = null;
            if (!string.IsNullOrWhiteSpace(sheet3Path))
            {
                sheet3 = await ReadSheetAsync(jobId, "sheet3", sheet3Path, cancellationToken);
                UpdateJob(jobId, RuntimeStage.Parsing, 90, "Third sheet parsed.");
            }

            await _jobs.SaveDatasetAsync(jobId, new StoredDataset
            {
                Sheet1 = sheet1,
                Sheet2 = sheet2,
                Sheet3 = sheet3
            }, cancellationToken);

            _jobs.TryUpdateUploadJob(jobId, j =>
            {
                j.Stage = RuntimeStage.Ready;
                j.Progress = 100;
                j.Message = "Dataset ready.";
                j.DatasetPath = Path.Combine(_jobs.GetJobDirectory(jobId), "dataset.json");
                var sheets = new List<ContractSheetInfo>
                {
                    new()
                    {
                        SheetName = "sheet1",
                        Columns = sheet1.Headers,
                        RowCount = sheet1.Rows.Count,
                        PreviewRows = sheet1.Rows.Take(50).Select(CloneRow).ToList()
                    }
                };
                if (sheet2 is not null)
                {
                    sheets.Add(new ContractSheetInfo
                    {
                        SheetName = "sheet2",
                        Columns = sheet2.Headers,
                        RowCount = sheet2.Rows.Count,
                        PreviewRows = sheet2.Rows.Take(50).Select(CloneRow).ToList()
                    });
                }
                if (sheet3 is not null)
                {
                    sheets.Add(new ContractSheetInfo
                    {
                        SheetName = "sheet3",
                        Columns = sheet3.Headers,
                        RowCount = sheet3.Rows.Count,
                        PreviewRows = sheet3.Rows.Take(50).Select(CloneRow).ToList()
                    });
                }

                j.Sheets = sheets;
            });

            await NotifyJobAsync(jobId);
        }
        catch (Exception ex)
        {
            _jobs.TryUpdateUploadJob(jobId, j =>
            {
                j.Stage = RuntimeStage.Failed;
                j.Progress = 100;
                j.Message = ex.Message;
            });

            await NotifyJobAsync(jobId);
        }
    }

    private async Task ExecuteQueryAsync(string jobId, string queryId, QueryExecuteRequest request, CancellationToken cancellationToken)
    {
        UpdateQuery(queryId, RuntimeStage.Executing, 10, "Executing query in sandbox host...");

        try
        {
            var dataset = await _jobs.LoadDatasetAsync(jobId, cancellationToken)
                ?? throw new InvalidOperationException("Dataset not found for job.");

            var payload = new SandboxRequestPayload
            {
                Code = request.Code,
                Sheet1 = new SandboxSheetPayload
                {
                    Headers = dataset.Sheet1.Headers,
                    Rows = dataset.Sheet1.Rows
                },
                Sheet2 = dataset.Sheet2 is null ? null : new SandboxSheetPayload
                {
                    Headers = dataset.Sheet2.Headers,
                    Rows = dataset.Sheet2.Rows
                },
                Sheet3 = dataset.Sheet3 is null ? null : new SandboxSheetPayload
                {
                    Headers = dataset.Sheet3.Headers,
                    Rows = dataset.Sheet3.Rows
                },
                MaxRows = Math.Clamp(request.MaxRows, 1, _options.HardMaxRows),
                HardLimitRows = _options.HardMaxRows,
                TimeoutMs = Math.Clamp(request.TimeoutMs, 100, _options.HardTimeoutMs)
            };

            var result = await _sandbox.ExecuteAsync(payload, cancellationToken);

            _jobs.TryUpdateQueryRun(queryId, q =>
            {
                q.Stage = result.Success ? RuntimeStage.Completed : RuntimeStage.Failed;
                q.Progress = 100;
                q.Message = result.Success ? "Query completed." : "Query failed.";
                q.Success = result.Success;
                q.Truncated = result.Truncated;
                q.ElapsedMs = result.ElapsedMs;
                q.Aggregate = result.Aggregate;
                q.Rows = result.Rows;
                q.Diagnostics = result.Diagnostics;
            });

            await NotifyQueryAsync(queryId);
        }
        catch (Exception ex)
        {
            _jobs.TryUpdateQueryRun(queryId, q =>
            {
                q.Stage = RuntimeStage.Failed;
                q.Progress = 100;
                q.Message = ex.Message;
                q.Success = false;
                q.Diagnostics =
                [
                    new RuntimeDiagnostic
                    {
                        Message = ex.Message,
                        Severity = "error",
                        Line = 1,
                        Column = 1
                    }
                ];
            });

            await NotifyQueryAsync(queryId);
        }
    }

    private async Task<StoredSheet> ReadSheetAsync(string jobId, string name, string path, CancellationToken cancellationToken)
    {
        var format = ToFormat(path);
        var reader = _readers.FirstOrDefault(x => x.CanRead(format))
            ?? throw new InvalidOperationException($"Reader not found for {format}.");

        await using var stream = File.OpenRead(path);
        var parsed = await reader.ReadAsync(stream, cancellationToken);
        var normalized = _normalizer.Normalize(parsed.Headers);

        var rows = new List<Dictionary<string, string?>>(parsed.Rows.Count);
        var headers = normalized.Select(x => x.NormalizedName).ToList();
        var total = Math.Max(1, parsed.Rows.Count);
        var nextProgressTick = 250;

        for (var rowIndex = 0; rowIndex < parsed.Rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceRow = parsed.Rows[rowIndex];
            var row = normalized.ToDictionary(
                column => column.NormalizedName,
                column => sourceRow.TryGetValue(column.OriginalName, out var value) ? value : null,
                StringComparer.OrdinalIgnoreCase);
            rows.Add(row);

            if (rows.Count >= nextProgressTick || rowIndex == parsed.Rows.Count - 1)
            {
                var ratio = (rowIndex + 1d) / total;
                var progress = name == "sheet1"
                    ? 10 + (int)Math.Round(ratio * 40d)
                    : name == "sheet2"
                        ? 50 + (int)Math.Round(ratio * 25d)
                        : 75 + (int)Math.Round(ratio * 20d);

                _jobs.TryUpdateSheetRows(jobId, name, headers, rows, completed: rowIndex == parsed.Rows.Count - 1);
                UpdateJob(jobId, RuntimeStage.Parsing, Math.Clamp(progress, 10, 95), $"Parsing {name}: {rowIndex + 1}/{parsed.Rows.Count} rows");
                nextProgressTick += 250;
            }
        }

        return new StoredSheet
        {
            Name = name,
            Headers = headers,
            Rows = rows
        };
    }

    private static SpreadsheetFormat ToFormat(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv" => SpreadsheetFormat.Csv,
            ".xlsx" => SpreadsheetFormat.Xlsx,
            _ => throw new InvalidOperationException("Unsupported file format.")
        };
    }

    private void UpdateJob(string jobId, RuntimeStage stage, int progress, string message)
    {
        _jobs.TryUpdateUploadJob(jobId, j =>
        {
            j.Stage = stage;
            j.Progress = progress;
            j.Message = message;
        });

        _ = NotifyJobAsync(jobId);
    }

    private void UpdateQuery(string queryId, RuntimeStage stage, int progress, string message)
    {
        _jobs.TryUpdateQueryRun(queryId, q =>
        {
            q.Stage = stage;
            q.Progress = progress;
            q.Message = message;
        });

        _ = NotifyQueryAsync(queryId);
    }

    private async Task NotifyJobAsync(string jobId)
    {
        var state = _jobs.GetUploadJob(jobId);
        if (state is null)
        {
            return;
        }

        await _hub.Clients.Group($"job:{jobId}").SendAsync("jobProgress", state);
    }

    private async Task NotifyQueryAsync(string queryId)
    {
        var state = _jobs.GetQueryRun(queryId);
        if (state is null)
        {
            return;
        }

        await _hub.Clients.Group($"query:{queryId}").SendAsync("queryProgress", state);
    }

    private static Dictionary<string, string?> CloneRow(Dictionary<string, string?> row)
    {
        return new Dictionary<string, string?>(row, StringComparer.OrdinalIgnoreCase);
    }
}
