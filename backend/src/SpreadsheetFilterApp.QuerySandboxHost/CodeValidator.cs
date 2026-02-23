using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace SpreadsheetFilterApp.QuerySandboxHost;

public sealed class CodeValidator
{
    private static readonly string[] ForbiddenTokens =
    [
        "System.IO", "File", "Directory", "Process", "Environment", "Reflection", "DllImport", "unsafe",
        "dynamic", "Activator", "Assembly", "HttpClient", "Socket", "Thread", "Task.Run", "GC", "Console", "stackalloc"
    ];

    private static readonly Regex[] ForbiddenTokenPatterns = ForbiddenTokens
        .Select(BuildForbiddenTokenPattern)
        .ToArray();
    
    private static readonly HashSet<string> AllowedLinqMethods =
    [
        "Where", "Select", "OrderBy", "OrderByDescending", "ThenBy", "ThenByDescending", "GroupBy", "GroupJoin", "Join",
        "Take", "Skip", "Distinct", "Count", "Any", "All", "Contains", "FirstOrDefault", "ToList"
    ];

    private static readonly HashSet<string> AllowedParseMethods =
    [
        "Parse", "TryParse"
    ];

    public IReadOnlyList<SandboxDiagnostic> Validate(string code)
    {
        var diagnostics = new List<SandboxDiagnostic>();

        ValidateTextual(code, diagnostics);
        ValidateSyntax(code, diagnostics);
        ValidateSemantic(code, diagnostics);

        return diagnostics;
    }

    private static void ValidateTextual(string code, List<SandboxDiagnostic> diagnostics)
    {
        for (var i = 0; i < ForbiddenTokens.Length; i++)
        {
            var token = ForbiddenTokens[i];
            var pattern = ForbiddenTokenPatterns[i];
            if (pattern.IsMatch(code))
            {
                diagnostics.Add(new SandboxDiagnostic
                {
                    Message = $"Forbidden token: {token}",
                    Severity = "error",
                    Line = 1,
                    Column = 1
                });
            }
        }

    }

    private static Regex BuildForbiddenTokenPattern(string token)
    {
        var escaped = Regex.Escape(token);
        var isWord = token.All(c => char.IsLetterOrDigit(c) || c == '_');
        var pattern = isWord
            ? $@"\b{escaped}\b"
            : $@"(?<!\w){escaped}(?!\w)";

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static void ValidateSyntax(string code, List<SandboxDiagnostic> diagnostics)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));
        var root = tree.GetRoot();

        foreach (var node in root.DescendantNodes())
        {
            if (node is UsingDirectiveSyntax
                or NamespaceDeclarationSyntax
                or FileScopedNamespaceDeclarationSyntax
                or ClassDeclarationSyntax
                or StructDeclarationSyntax
                or ForStatementSyntax
                or ForEachStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or SwitchStatementSyntax
                or TryStatementSyntax
                or LockStatementSyntax
                or UnsafeStatementSyntax
                or FixedStatementSyntax)
            {
                AddNodeError(diagnostics, node, $"Syntax not allowed: {node.Kind()}");
            }

            if (node is ObjectCreationExpressionSyntax objectCreation && !IsAllowedObjectCreation(objectCreation))
            {
                AddNodeError(diagnostics, node, "Object creation is not allowed.");
            }
        }
    }

    private static void ValidateSemantic(string code, List<SandboxDiagnostic> diagnostics)
    {
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
        var compilation = script.GetCompilation();
        var tree = compilation.SyntaxTrees.Single();
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol is null)
            {
                continue;
            }

            if (IsAllowedInvocation(symbol))
            {
                continue;
            }

            AddNodeError(diagnostics, invocation, $"Method not allowed: {symbol.ContainingType?.ToDisplayString()}.{symbol.Name}");
        }

        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var symbol = model.GetSymbolInfo(memberAccess).Symbol;
            if (symbol is null)
            {
                continue;
            }

            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (ns.StartsWith("System.IO", StringComparison.Ordinal)
                || ns.StartsWith("System.Net", StringComparison.Ordinal)
                || ns.StartsWith("System.Reflection", StringComparison.Ordinal)
                || ns.StartsWith("System.Diagnostics", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft", StringComparison.Ordinal))
            {
                AddNodeError(diagnostics, memberAccess, $"Namespace not allowed: {ns}");
            }
        }
    }

    private static bool IsAllowedInvocation(IMethodSymbol symbol)
    {
        var containingType = symbol.ContainingType?.ToDisplayString() ?? string.Empty;
        var containingTypeName = symbol.ContainingType?.Name ?? string.Empty;

        // Allow helper methods declared inside the user script submission itself.
        if (containingTypeName.StartsWith("Submission#", StringComparison.Ordinal))
        {
            return true;
        }

        if (containingType == typeof(RowRef).FullName)
        {
            return symbol.Name is "Str" or "Int" or "Dbl" or "Date" or "Norm" or "EqualsNorm";
        }

        if (containingType == typeof(RowValue).FullName)
        {
            return symbol.Name is "normalize" or "Normalize";
        }

        if (symbol.ContainingType?.SpecialType == SpecialType.System_String
            || containingType == typeof(string).FullName
            || containingType == "string")
        {
            return symbol.Name is
                "IsNullOrEmpty" or
                "IsNullOrWhiteSpace" or
                "Contains" or
                "StartsWith" or
                "EndsWith" or
                "Equals";
        }

        if (containingType is "System.Int32" or "System.Double" or "System.Decimal" or "System.DateTime")
        {
            return AllowedParseMethods.Contains(symbol.Name);
        }

        if (symbol.IsExtensionMethod && symbol.ContainingNamespace.ToDisplayString().StartsWith("System.Linq", StringComparison.Ordinal))
        {
            return AllowedLinqMethods.Contains(symbol.Name);
        }

        if (containingType.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal))
        {
            return symbol.Name == "Add";
        }

        return false;
    }

    private static bool IsAllowedObjectCreation(ObjectCreationExpressionSyntax node)
    {
        var typeText = node.Type.ToString();
        return typeText.StartsWith("Dictionary<", StringComparison.Ordinal)
               || typeText.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal);
    }

    private static void AddNodeError(List<SandboxDiagnostic> diagnostics, SyntaxNode node, string message)
    {
        var span = node.GetLocation().GetLineSpan();
        diagnostics.Add(new SandboxDiagnostic
        {
            Message = message,
            Severity = "error",
            Line = span.StartLinePosition.Line + 1,
            Column = span.StartLinePosition.Character + 1
        });
    }
}
