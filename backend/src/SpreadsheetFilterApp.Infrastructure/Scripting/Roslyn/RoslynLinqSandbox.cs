using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using SpreadsheetFilterApp.Application.Abstractions.Scripting;
using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Infrastructure.Scripting.Roslyn;

public sealed class RoslynLinqSandbox(
    ScriptOptionsFactory scriptOptionsFactory,
    SandboxSecurityPolicy sandboxSecurityPolicy) : ILinqSandbox
{
    private const int MaxRows = 300000;
    private const int BaseTimeoutSeconds = 10;
    private const int MaxTimeoutSeconds = 45;

    private readonly ScriptOptionsFactory _scriptOptionsFactory = scriptOptionsFactory;
    private readonly SandboxSecurityPolicy _sandboxSecurityPolicy = sandboxSecurityPolicy;

    public async Task<LinqExecutionResult> ExecuteAsync(
        IReadOnlyList<ColumnSchemaDto> schema,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        string linqCode,
        int? maxResultRows,
        CancellationToken cancellationToken)
    {
        _sandboxSecurityPolicy.AssertSafe(linqCode);

        if (rows.Count > MaxRows)
        {
            throw new InvalidOperationException($"Max rows exceeded ({MaxRows}).");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var rowType = RuntimeRowFactory.BuildRowType(schema);
        var typedRows = RuntimeRowFactory.CreateRowInstances(rowType, schema, rows);

        var globalsType = typeof(SandboxGlobals<>).MakeGenericType(rowType);
        var globals = Activator.CreateInstance(globalsType)!;
        globalsType.GetProperty(nameof(SandboxGlobals<object>.rows))!.SetValue(globals, typedRows);

        var scriptOptions = _scriptOptionsFactory.Create(rowType.Assembly);
        var script = CSharpScript.Create<object>(linqCode, scriptOptions, globalsType);
        var diagnostics = script.Compile();
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(x => x.GetMessage())));
        }

        var timeoutSeconds = Math.Min(MaxTimeoutSeconds, BaseTimeoutSeconds + ((rows.Count / 20000) * 5));
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var resultState = await script.RunAsync(globals, linkedCts.Token);
        if (resultState.ReturnValue is not IEnumerable enumerable)
        {
            throw new InvalidOperationException("LINQ expression must return an IEnumerable.");
        }

        var mapped = MapResultRows(enumerable, rowType, schema, maxResultRows);
        stopwatch.Stop();

        return new LinqExecutionResult
        {
            Headers = mapped.Headers,
            Rows = mapped.Rows,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows) MapResultRows(
        IEnumerable enumerable,
        Type rowType,
        IReadOnlyList<ColumnSchemaDto> schema,
        int? maxResultRows)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();

        foreach (var item in enumerable)
        {
            if (item is null)
            {
                continue;
            }

            if (item.GetType() == rowType)
            {
                var dict = schema.ToDictionary(
                    x => x.NormalizedName,
                    x => rowType.GetProperty(x.NormalizedName)?.GetValue(item));
                rows.Add(dict);
            }
            else if (item is IDictionary<string, object?> typedDict)
            {
                rows.Add(new Dictionary<string, object?>(typedDict, StringComparer.OrdinalIgnoreCase));
            }
            else if (item is IDictionary anyDict)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DictionaryEntry entry in anyDict)
                {
                    var key = entry.Key?.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        dict[key] = entry.Value;
                    }
                }

                rows.Add(dict);
            }
            else
            {
                var props = item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                var propDict = props.ToDictionary(p => p.Name, p => p.GetValue(item), StringComparer.OrdinalIgnoreCase);
                rows.Add(propDict);
            }

            if (maxResultRows.HasValue && rows.Count >= maxResultRows.Value)
            {
                break;
            }
        }

        var headers = rows.Count == 0
            ? schema.Select(x => x.NormalizedName).ToList()
            : rows.SelectMany(x => x.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return (headers, rows);
    }
}

public sealed class SandboxGlobals<TRow>
{
    public IEnumerable<TRow> rows { get; set; } = [];
}
