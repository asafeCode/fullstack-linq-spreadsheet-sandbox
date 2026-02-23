using System.Collections;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace SpreadsheetFilterApp.QuerySandboxHost;

public sealed class QueryRunner
{
    public async Task<SandboxResponse> RunAsync(SandboxRequest request, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var validator = new CodeValidator();
        var validation = validator.Validate(request.Code);
        if (validation.Count > 0)
        {
            return new SandboxResponse
            {
                Success = false,
                Diagnostics = validation.ToList(),
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }

        var code = request.Code.Contains("return ", StringComparison.Ordinal) ? request.Code : $"return {request.Code}";
        var maxRows = Math.Clamp(request.MaxRows, 1, request.HardLimitRows);

        var sheet1 = BuildSheet("sheet1", request.Sheet1);
        var sheet2 = request.Sheet2 is null ? null : BuildSheet("sheet2", request.Sheet2);
        var sheet3 = request.Sheet3 is null ? null : BuildSheet("sheet3", request.Sheet3);

        var globals = new SandboxGlobals
        {
            rows = sheet1.Rows,
            sheet1 = sheet1,
            sheet2 = sheet2,
            sheet3 = sheet3
        };

        var scriptOptions = ScriptOptions.Default
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(List<>).Assembly,
                typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly,
                typeof(RowRef).Assembly)
            .WithImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "SpreadsheetFilterApp.QuerySandboxHost");

        var script = CSharpScript.Create<object>(code, scriptOptions, typeof(SandboxGlobals));
        var compileDiagnostics = script.Compile().Where(x => x.Severity == DiagnosticSeverity.Error).ToList();
        if (compileDiagnostics.Count > 0)
        {
            return new SandboxResponse
            {
                Success = false,
                Diagnostics = compileDiagnostics.Select(ToDiagnostic).ToList(),
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }

        var timeoutMs = Math.Clamp(request.TimeoutMs, 100, 10000);
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var state = await script.RunAsync(globals, linkedCts.Token);
            var mapped = MapResult(state.ReturnValue, maxRows);

            return new SandboxResponse
            {
                Success = true,
                Rows = mapped.Rows,
                Aggregate = mapped.Aggregate,
                Truncated = mapped.Truncated,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            return new SandboxResponse
            {
                Success = false,
                Diagnostics =
                [
                    new SandboxDiagnostic
                    {
                        Message = "Query execution timeout exceeded.",
                        Severity = "error",
                        Line = 1,
                        Column = 1
                    }
                ],
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            return new SandboxResponse
            {
                Success = false,
                Diagnostics =
                [
                    new SandboxDiagnostic
                    {
                        Message = ex.Message,
                        Severity = "error",
                        Line = 1,
                        Column = 1
                    }
                ],
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    private static Sheet BuildSheet(string name, SheetPayload payload)
    {
        var rows = payload.Rows
            .Select(r => (dynamic)new RowRef(r))
            .ToList();

        return new Sheet
        {
            Name = name,
            Rows = rows
        };
    }

    private static (List<Dictionary<string, object?>> Rows, object? Aggregate, bool Truncated) MapResult(object? value, int maxRows)
    {
        if (value is null)
        {
            return ([], null, false);
        }

        if (value is not IEnumerable enumerable || value is string)
        {
            return ([], value, false);
        }

        var rows = new List<Dictionary<string, object?>>();
        var truncated = false;

        foreach (var item in enumerable)
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }

            if (item is null)
            {
                continue;
            }

            rows.Add(MapRow(item));
        }

        return (rows, null, truncated);
    }

    private static Dictionary<string, object?> MapRow(object item)
    {
        if (item is IDictionary<string, object?> dict)
        {
            return dict.ToDictionary(x => x.Key, x => NormalizeOutputValue(x.Value), StringComparer.OrdinalIgnoreCase);
        }

        if (item is IDictionary anyDict)
        {
            var mapped = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in anyDict)
            {
                if (entry.Key?.ToString() is { Length: > 0 } key)
                {
                    mapped[key] = entry.Value;
                }
            }

            return mapped;
        }

        if (item is RowRef rowRef)
        {
            return rowRef.ToDictionary();
        }

        var props = item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return props.ToDictionary(x => x.Name, x => NormalizeOutputValue(x.GetValue(item)), StringComparer.OrdinalIgnoreCase);
    }

    private static object? NormalizeOutputValue(object? value)
    {
        return value is RowValue rowValue ? rowValue.Raw : value;
    }

    private static SandboxDiagnostic ToDiagnostic(Diagnostic d)
    {
        var span = d.Location.GetLineSpan();
        return new SandboxDiagnostic
        {
            Message = d.GetMessage(),
            Severity = "error",
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1
        };
    }
}
