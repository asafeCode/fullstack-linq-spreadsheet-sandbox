using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

public enum RuntimeStage
{
    Queued,
    Parsing,
    Ready,
    Executing,
    Completed,
    Failed
}

public sealed class UploadJobState
{
    public required string JobId { get; init; }
    public RuntimeStage Stage { get; set; } = RuntimeStage.Queued;
    public int Progress { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DatasetPath { get; set; }
    public List<ContractSheetInfo> Sheets { get; set; } = [];
    public List<UploadSheetStatus> SheetStatuses { get; set; } = [];
    [JsonIgnore]
    public Dictionary<string, StoredSheet> WorkingSheets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class UploadSheetStatus
{
    public required string SheetName { get; init; }
    public int AvailableRows { get; set; }
    public int TotalRows { get; set; }
    public bool Completed { get; set; }
}

public sealed class QueryRunState
{
    public required string QueryId { get; init; }
    public required string JobId { get; init; }
    public RuntimeStage Stage { get; set; } = RuntimeStage.Queued;
    public int Progress { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public bool Truncated { get; set; }
    public long ElapsedMs { get; set; }
    public object? Aggregate { get; set; }
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
    public List<RuntimeDiagnostic> Diagnostics { get; set; } = [];
}

public sealed class RuntimeDiagnostic
{
    public required string Message { get; init; }
    public required string Severity { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
}

public sealed class ContractSheetInfo
{
    public required string SheetName { get; init; }
    public required List<string> Columns { get; init; }
    public int RowCount { get; init; }
    public List<Dictionary<string, string?>> PreviewRows { get; init; } = [];
}

public sealed class StoredDataset
{
    public required StoredSheet Sheet1 { get; init; }
    public StoredSheet? Sheet2 { get; init; }
    public StoredSheet? Sheet3 { get; init; }
}

public sealed class StoredSheet
{
    public required string Name { get; init; }
    public required List<string> Headers { get; init; }
    public required List<Dictionary<string, string?>> Rows { get; init; }
}

public sealed class QueryExecuteRequest
{
    public required string Code { get; init; }
    public int MaxRows { get; init; } = 2000;
    public int TimeoutMs { get; init; } = 2000;
}

public sealed class UnifySheetsRequest
{
    public required string PrimarySheetName { get; init; }
    public required string PrimaryKeyColumn { get; init; }
    public required List<UnifyComparisonRequest> Comparisons { get; init; }
}

public sealed class UnifyComparisonRequest
{
    public required string SheetName { get; init; }
    public required string CompareColumn { get; init; }
}

public sealed class QueryContractResponse
{
    public required List<string> Variables { get; init; }
    public required List<string> AllowedMethods { get; init; }
    public required List<string> Snippets { get; init; }
    public List<ContractSheetInfo> Sheets { get; init; } = [];
}

public sealed class PreviewPageResponse
{
    public required string SheetName { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRows { get; init; }
    public required List<Dictionary<string, string?>> Rows { get; init; }
}

public sealed class SandboxRequestPayload
{
    public required string Code { get; init; }
    public required SandboxSheetPayload Sheet1 { get; init; }
    public SandboxSheetPayload? Sheet2 { get; init; }
    public SandboxSheetPayload? Sheet3 { get; init; }
    public int MaxRows { get; init; }
    public int HardLimitRows { get; init; }
    public int TimeoutMs { get; init; }
}

public sealed class SandboxSheetPayload
{
    public required List<string> Headers { get; init; }
    public required List<Dictionary<string, string?>> Rows { get; init; }
}

public sealed class SandboxResponsePayload
{
    public bool Success { get; init; }
    public List<RuntimeDiagnostic> Diagnostics { get; init; } = [];
    public List<Dictionary<string, object?>> Rows { get; init; } = [];
    public object? Aggregate { get; init; }
    public bool Truncated { get; init; }
    public long ElapsedMs { get; init; }
}
