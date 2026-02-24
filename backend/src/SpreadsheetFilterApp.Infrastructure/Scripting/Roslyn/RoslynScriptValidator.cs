using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using SpreadsheetFilterApp.Application.Abstractions.Scripting;
using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Infrastructure.Scripting.Roslyn;

public sealed class RoslynScriptValidator(
    ScriptOptionsFactory scriptOptionsFactory,
    SandboxSecurityPolicy sandboxSecurityPolicy) : IScriptValidator
{
    private readonly ScriptOptionsFactory _scriptOptionsFactory = scriptOptionsFactory;
    private readonly SandboxSecurityPolicy _sandboxSecurityPolicy = sandboxSecurityPolicy;

    public Task<ValidationResultDto> ValidateAsync(
        IReadOnlyList<ColumnSchemaDto> schema,
        string linqCode,
        CancellationToken cancellationToken)
    {
        _sandboxSecurityPolicy.AssertSafe(linqCode);

        var rowType = RuntimeRowFactory.BuildRowType(schema);
        var globalsType = typeof(SandboxGlobals<>).MakeGenericType(rowType);
        var scriptOptions = _scriptOptionsFactory.Create(rowType.Assembly);
        var script = CSharpScript.Create<object>(linqCode, scriptOptions, globalsType);
        var diagnostics = script.Compile();

        var mapped = diagnostics
            .Where(x => x.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(x =>
            {
                var span = x.Location.GetLineSpan();
                return new ValidationDiagnosticDto
                {
                    Message = x.GetMessage(),
                    Severity = x.Severity == DiagnosticSeverity.Error ? "error" : "warning",
                    Line = span.StartLinePosition.Line + 1,
                    Column = span.StartLinePosition.Character + 1
                };
            })
            .ToList();

        return Task.FromResult(new ValidationResultDto
        {
            Diagnostics = mapped
        });
    }
}
