namespace SpreadsheetFilterApp.Web.QueryRuntime;

public sealed class QueryRuntimeOptions
{
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "SpreadsheetFilterApp.QueryRuntime");
    public int JobTtlMinutes { get; set; } = 60;
    public int MaxFileMb { get; set; } = 40;
    public int MaxConcurrentJobs { get; set; } = 1;
    public int DefaultMaxRows { get; set; } = 2000;
    public int HardMaxRows { get; set; } = 10000;
    public int DefaultTimeoutMs { get; set; } = 2000;
    public int HardTimeoutMs { get; set; } = 10000;
    public string? SandboxHostDllPath { get; set; }
}
