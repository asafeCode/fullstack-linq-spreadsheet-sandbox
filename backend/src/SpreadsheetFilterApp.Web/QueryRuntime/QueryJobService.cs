using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

public interface IQueryJobService
{
    Task<string> CreateUploadJobAsync(IFormFile file1, IFormFile? file2, IFormFile? file3, CancellationToken cancellationToken);
    UploadJobState? GetUploadJob(string jobId);
    QueryRunState? GetQueryRun(string queryId);
    Task<string> EnqueueQueryAsync(string jobId, QueryExecuteRequest request, CancellationToken cancellationToken);
    Task<ContractSheetInfo> UnifySheetsAsync(string jobId, UnifySheetsRequest request, CancellationToken cancellationToken);
    QueryContractResponse GetContract(string? jobId);
    PreviewPageResponse? GetPreviewPage(string jobId, string sheetName, int page, int pageSize);
    string GetJobDirectory(string jobId);
    Task SaveDatasetAsync(string jobId, StoredDataset dataset, CancellationToken cancellationToken);
    Task<StoredDataset?> LoadDatasetAsync(string jobId, CancellationToken cancellationToken);
    bool TryUpdateUploadJob(string jobId, Action<UploadJobState> update);
    bool TryUpdateQueryRun(string queryId, Action<QueryRunState> update);
    bool TryUpdateSheetRows(string jobId, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<Dictionary<string, string?>> rows, bool completed);
}

public sealed class QueryJobService : IQueryJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, UploadJobState> _uploadJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, QueryRunState> _queryRuns = new(StringComparer.OrdinalIgnoreCase);
    private readonly IQueryWorkQueue _queue;
    private readonly QueryRuntimeOptions _options;

    public QueryJobService(IQueryWorkQueue queue, IOptions<QueryRuntimeOptions> options)
    {
        _queue = queue;
        _options = options.Value;
        Directory.CreateDirectory(_options.RootPath);
    }

    public async Task<string> CreateUploadJobAsync(IFormFile file1, IFormFile? file2, IFormFile? file3, CancellationToken cancellationToken)
    {
        ValidateFile(file1);
        if (file2 is not null)
        {
            ValidateFile(file2);
        }

        if (file3 is not null)
        {
            ValidateFile(file3);
        }

        var jobId = Guid.NewGuid().ToString("N");
        var dir = GetJobDirectory(jobId);
        Directory.CreateDirectory(dir);

        await SaveFormFileAsync(file1, Path.Combine(dir, "sheet1" + Path.GetExtension(file1.FileName).ToLowerInvariant()), cancellationToken);
        if (file2 is not null)
        {
            await SaveFormFileAsync(file2, Path.Combine(dir, "sheet2" + Path.GetExtension(file2.FileName).ToLowerInvariant()), cancellationToken);
        }

        if (file3 is not null)
        {
            await SaveFormFileAsync(file3, Path.Combine(dir, "sheet3" + Path.GetExtension(file3.FileName).ToLowerInvariant()), cancellationToken);
        }

        _uploadJobs[jobId] = new UploadJobState
        {
            JobId = jobId,
            Stage = RuntimeStage.Queued,
            Progress = 0,
            Message = "Job queued"
        };

        await _queue.EnqueueAsync(new QueryWorkItem
        {
            Kind = QueryWorkKind.ParseUpload,
            JobId = jobId
        }, cancellationToken);

        return jobId;
    }

    public UploadJobState? GetUploadJob(string jobId)
    {
        _uploadJobs.TryGetValue(jobId, out var state);
        return state;
    }

    public QueryRunState? GetQueryRun(string queryId)
    {
        _queryRuns.TryGetValue(queryId, out var state);
        return state;
    }

    public async Task<string> EnqueueQueryAsync(string jobId, QueryExecuteRequest request, CancellationToken cancellationToken)
    {
        if (!_uploadJobs.TryGetValue(jobId, out var upload) || upload.Stage != RuntimeStage.Ready)
        {
            throw new InvalidOperationException("Upload job is not ready.");
        }

        var queryId = Guid.NewGuid().ToString("N");
        _queryRuns[queryId] = new QueryRunState
        {
            QueryId = queryId,
            JobId = jobId,
            Stage = RuntimeStage.Queued,
            Progress = 0,
            Message = "Query queued"
        };

        await _queue.EnqueueAsync(new QueryWorkItem
        {
            Kind = QueryWorkKind.ExecuteQuery,
            JobId = jobId,
            QueryId = queryId,
            Query = request
        }, cancellationToken);

        return queryId;
    }

    public async Task<ContractSheetInfo> UnifySheetsAsync(string jobId, UnifySheetsRequest request, CancellationToken cancellationToken)
    {
        if (!_uploadJobs.TryGetValue(jobId, out var upload) || upload.Stage != RuntimeStage.Ready)
        {
            throw new InvalidOperationException("Upload job is not ready.");
        }

        if (request.Comparisons.Count == 0)
        {
            throw new InvalidOperationException("At least one child sheet comparison is required.");
        }

        var dataset = await LoadDatasetAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException("Dataset not found for job.");

        var allSheets = new List<StoredSheet> { dataset.Sheet1 };
        if (dataset.Sheet2 is not null)
        {
            allSheets.Add(dataset.Sheet2);
        }

        if (dataset.Sheet3 is not null)
        {
            allSheets.Add(dataset.Sheet3);
        }

        var primary = allSheets.FirstOrDefault(s => string.Equals(s.Name, request.PrimarySheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Primary sheet not found.");

        if (!primary.Headers.Any(h => string.Equals(h, request.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Primary key column was not found on primary sheet.");
        }

        var invalidComparison = request.Comparisons.FirstOrDefault(c =>
        {
            var sheet = allSheets.FirstOrDefault(s => string.Equals(s.Name, c.SheetName, StringComparison.OrdinalIgnoreCase));
            return sheet is null || !sheet.Headers.Any(h => string.Equals(h, c.CompareColumn, StringComparison.OrdinalIgnoreCase));
        });

        if (invalidComparison is not null)
        {
            throw new InvalidOperationException($"Invalid sheet/column mapping: {invalidComparison.SheetName}.{invalidComparison.CompareColumn}");
        }

        var childLookups = request.Comparisons
            .Select(c =>
            {
                var sheet = allSheets.First(s => string.Equals(s.Name, c.SheetName, StringComparison.OrdinalIgnoreCase));
                var byKey = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in sheet.Rows)
                {
                    var key = NormalizeText(ReadValueIgnoreCase(row, c.CompareColumn));
                    if (key.Length == 0 || byKey.ContainsKey(key))
                    {
                        continue;
                    }

                    byKey[key] = row;
                }

                return (sheet, compareColumn: c.CompareColumn, byKey);
            })
            .ToList();

        var unifiedHeaders = new List<string>(primary.Headers);
        foreach (var mapping in childLookups)
        {
            foreach (var header in mapping.sheet.Headers)
            {
                if (string.Equals(header, mapping.compareColumn, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var candidate = $"{mapping.sheet.Name}__{header}";
                if (!unifiedHeaders.Any(h => string.Equals(h, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    unifiedHeaders.Add(candidate);
                }
            }
        }

        var unifiedRows = new List<Dictionary<string, string?>>();
        var seenPrimaryKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var primaryRow in primary.Rows)
        {
            var primaryKey = NormalizeText(ReadValueIgnoreCase(primaryRow, request.PrimaryKeyColumn));
            if (primaryKey.Length == 0 || !seenPrimaryKeys.Add(primaryKey))
            {
                continue;
            }

            var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in primary.Headers)
            {
                merged[header] = ReadValueIgnoreCase(primaryRow, header);
            }

            foreach (var mapping in childLookups)
            {
                if (!mapping.byKey.TryGetValue(primaryKey, out var childRow))
                {
                    continue;
                }

                foreach (var header in mapping.sheet.Headers)
                {
                    if (string.Equals(header, mapping.compareColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var destColumn = $"{mapping.sheet.Name}__{header}";
                    merged[destColumn] = ReadValueIgnoreCase(childRow, header);
                }
            }

            unifiedRows.Add(merged);
        }

        var unifiedSheet = new StoredSheet
        {
            Name = "unified",
            Headers = unifiedHeaders,
            Rows = unifiedRows
        };

        await SaveDatasetAsync(jobId, new StoredDataset
        {
            Sheet1 = unifiedSheet,
            Sheet2 = dataset.Sheet2,
            Sheet3 = dataset.Sheet3
        }, cancellationToken);

        upload.WorkingSheets[unifiedSheet.Name] = unifiedSheet;
        upload.Sheets = upload.Sheets
            .Where(s => !string.Equals(s.SheetName, unifiedSheet.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        upload.Sheets.Insert(0, new ContractSheetInfo
        {
            SheetName = unifiedSheet.Name,
            Columns = unifiedSheet.Headers,
            RowCount = unifiedSheet.Rows.Count,
            PreviewRows = unifiedSheet.Rows.Take(50).Select(CloneRow).ToList()
        });
        upload.UpdatedAt = DateTimeOffset.UtcNow;
        upload.Message = "Unified sheet ready.";

        return new ContractSheetInfo
        {
            SheetName = unifiedSheet.Name,
            Columns = unifiedSheet.Headers,
            RowCount = unifiedSheet.Rows.Count,
            PreviewRows = unifiedSheet.Rows.Take(50).Select(CloneRow).ToList()
        };
    }

    public QueryContractResponse GetContract(string? jobId)
    {
        var sheets = new List<ContractSheetInfo>();
        if (!string.IsNullOrWhiteSpace(jobId) && _uploadJobs.TryGetValue(jobId, out var job) && job.Sheets.Count > 0)
        {
            sheets.AddRange(job.Sheets);
        }

        return new QueryContractResponse
        {
            Variables = ["rows", "sheet1", "sheet2", "sheet3"],
            AllowedMethods =
            [
                "Where", "Select", "OrderBy", "ThenBy", "GroupBy", "GroupJoin", "Join", "Take", "Skip", "Distinct", "Count", "Any", "All", "Contains", "FirstOrDefault", "ToList"
            ],
            Snippets =
            [
                "return rows.Where(r => r.Str(\"status_matricula\") == \"Ativa\").Take(200).ToList();",
                "return rows.Where(r => r.nome.normalize() == \"abner da silva costa\".normalize()).ToList();",
                "return rows.Where(r => r.EqualsNorm(\"nome\", \"Abner da Silva Costa\")).Take(200).ToList();",
                "return rows.GroupBy(r => r.Str(\"status_matricula\")).Select(g => new { status = g.Key, total = g.Count() }).ToList();"
            ],
            Sheets = sheets
        };
    }

    public PreviewPageResponse? GetPreviewPage(string jobId, string sheetName, int page, int pageSize)
    {
        if (!_uploadJobs.TryGetValue(jobId, out var job))
        {
            return null;
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 1000);

        if (job.WorkingSheets.TryGetValue(sheetName, out var workingSheet))
        {
            return BuildPage(sheetName, workingSheet.Rows, safePage, safePageSize);
        }

        var contractSheet = job.Sheets.FirstOrDefault(s => string.Equals(s.SheetName, sheetName, StringComparison.OrdinalIgnoreCase));
        if (contractSheet is null)
        {
            return null;
        }

        return BuildPage(sheetName, contractSheet.PreviewRows, safePage, safePageSize, contractSheet.RowCount);
    }

    public string GetJobDirectory(string jobId) => Path.Combine(_options.RootPath, jobId);

    public async Task SaveDatasetAsync(string jobId, StoredDataset dataset, CancellationToken cancellationToken)
    {
        var path = Path.Combine(GetJobDirectory(jobId), "dataset.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dataset, JsonOptions), cancellationToken);
    }

    public async Task<StoredDataset?> LoadDatasetAsync(string jobId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(GetJobDirectory(jobId), "dataset.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<StoredDataset>(json, JsonOptions);
    }

    public bool TryUpdateUploadJob(string jobId, Action<UploadJobState> update)
    {
        if (!_uploadJobs.TryGetValue(jobId, out var state))
        {
            return false;
        }

        update(state);
        state.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public bool TryUpdateQueryRun(string queryId, Action<QueryRunState> update)
    {
        if (!_queryRuns.TryGetValue(queryId, out var state))
        {
            return false;
        }

        update(state);
        state.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public bool TryUpdateSheetRows(string jobId, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<Dictionary<string, string?>> rows, bool completed)
    {
        if (!_uploadJobs.TryGetValue(jobId, out var job))
        {
            return false;
        }

        var key = sheetName.Trim();
        if (key.Length == 0)
        {
            return false;
        }

        var materializedRows = rows as List<Dictionary<string, string?>> ?? rows.ToList();
        var snapshot = new StoredSheet
        {
            Name = key,
            Headers = headers.ToList(),
            Rows = materializedRows
        };

        job.WorkingSheets[key] = snapshot;
        var status = job.SheetStatuses.FirstOrDefault(x => string.Equals(x.SheetName, key, StringComparison.OrdinalIgnoreCase));
        if (status is null)
        {
            status = new UploadSheetStatus
            {
                SheetName = key
            };
            job.SheetStatuses.Add(status);
        }

        status.AvailableRows = rows.Count;
        status.TotalRows = completed ? rows.Count : Math.Max(status.TotalRows, rows.Count);
        status.Completed = completed;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    private void ValidateFile(IFormFile file)
    {
        var maxBytes = _options.MaxFileMb * 1024L * 1024L;
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("File cannot be empty.");
        }

        if (file.Length > maxBytes)
        {
            throw new InvalidOperationException($"Max file size is {_options.MaxFileMb} MB.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".csv" and not ".xlsx")
        {
            throw new InvalidOperationException("Only CSV/XLSX files are supported.");
        }
    }

    private static async Task SaveFormFileAsync(IFormFile file, string path, CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        await using var target = File.Create(path);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static PreviewPageResponse BuildPage(
        string sheetName,
        IReadOnlyList<Dictionary<string, string?>> rows,
        int page,
        int pageSize,
        int? totalRows = null)
    {
        var skip = (page - 1) * pageSize;
        var pageRows = rows.Skip(skip).Take(pageSize)
            .Select(r => new Dictionary<string, string?>(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new PreviewPageResponse
        {
            SheetName = sheetName,
            Page = page,
            PageSize = pageSize,
            TotalRows = totalRows ?? rows.Count,
            Rows = pageRows
        };
    }

    private static string? ReadValueIgnoreCase(IReadOnlyDictionary<string, string?> row, string key)
    {
        if (row.TryGetValue(key, out var direct))
        {
            return direct;
        }

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string NormalizeText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, string?> CloneRow(Dictionary<string, string?> row)
    {
        return new Dictionary<string, string?>(row, StringComparer.OrdinalIgnoreCase);
    }
}
