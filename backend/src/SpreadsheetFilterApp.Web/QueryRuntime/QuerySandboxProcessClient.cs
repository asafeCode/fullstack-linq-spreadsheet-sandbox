using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SpreadsheetFilterApp.Web.QueryRuntime;

public interface IQuerySandboxProcessClient
{
    Task<SandboxResponsePayload> ExecuteAsync(SandboxRequestPayload request, CancellationToken cancellationToken);
}

public sealed class QuerySandboxProcessClient : IQuerySandboxProcessClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly QueryRuntimeOptions _options;
    private readonly IWebHostEnvironment _environment;

    public QuerySandboxProcessClient(IOptions<QueryRuntimeOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<SandboxResponsePayload> ExecuteAsync(SandboxRequestPayload request, CancellationToken cancellationToken)
    {
        var hostDll = ResolveHostDllPath();
        if (!File.Exists(hostDll))
        {
            throw new InvalidOperationException($"Sandbox host not found: {hostDll}");
        }

        var runDir = Path.Combine(Path.GetTempPath(), "SpreadsheetFilterApp.QuerySandboxRuns", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDir);

        var inputPath = Path.Combine(runDir, "input.json");
        var outputPath = Path.Combine(runDir, "output.json");
        await File.WriteAllTextAsync(inputPath, JsonSerializer.Serialize(request, JsonOptions), cancellationToken);

        var timeoutMs = Math.Clamp(request.TimeoutMs, 100, _options.HardTimeoutMs) + 250;
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{hostDll}\" \"{inputPath}\" \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            },
            EnableRaisingEvents = true
        };

        process.Start();
        var exited = await process.WaitForExitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("Sandbox execution timed out.");
        }

        if (!File.Exists(outputPath))
        {
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Sandbox did not produce output. {stderr}");
        }

        var json = await File.ReadAllTextAsync(outputPath, cancellationToken);
        var response = JsonSerializer.Deserialize<SandboxResponsePayload>(json, JsonOptions)
            ?? throw new InvalidOperationException("Sandbox returned invalid payload.");

        try
        {
            Directory.Delete(runDir, recursive: true);
        }
        catch
        {
            // no-op
        }

        return response;
    }

    private string ResolveHostDllPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.SandboxHostDllPath))
        {
            return _options.SandboxHostDllPath;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "SpreadsheetFilterApp.QuerySandboxHost", "bin", "Debug", "net10.0", "SpreadsheetFilterApp.QuerySandboxHost.dll"));
    }
}

file static class ProcessExtensions
{
    public static async Task<bool> WaitForExitAsync(this Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
