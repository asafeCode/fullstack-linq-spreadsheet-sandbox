using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SpreadsheetFilterApp.Application.DTOs;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SpreadsheetFilterApp.Infrastructure.Scripting.Roslyn;

internal static class RuntimeRowFactory
{
    private static readonly string DynamicAssemblyRoot = Path.Combine(Path.GetTempPath(), "SpreadsheetFilterApp", "DynamicRows");

    static RuntimeRowFactory()
    {
        Directory.CreateDirectory(DynamicAssemblyRoot);
    }

    public static Type BuildRowType(IReadOnlyList<ColumnSchemaDto> schema)
    {
        var source = BuildSource(schema);
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>();

        var compilation = CSharpCompilation.Create(
            $"DynamicRows_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var assemblyPath = Path.Combine(DynamicAssemblyRoot, $"DynamicRows_{Guid.NewGuid():N}.dll");
        var emit = default(Microsoft.CodeAnalysis.Emit.EmitResult);
        using (var fileStream = File.Create(assemblyPath))
        {
            emit = compilation.Emit(fileStream);
        }

        if (!emit.Success)
        {
            var message = string.Join(Environment.NewLine, emit.Diagnostics.Select(x => x.ToString()));
            throw new InvalidOperationException($"Could not build dynamic row type. {message}");
        }

        var assembly = Assembly.LoadFrom(assemblyPath);
        return assembly.GetType("SpreadsheetFilterApp.Dynamic.Row")
            ?? throw new InvalidOperationException("Dynamic row type not found.");
    }

    public static IList CreateRowInstances(
        Type rowType,
        IReadOnlyList<ColumnSchemaDto> schema,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        var listType = typeof(List<>).MakeGenericType(rowType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var row in rows)
        {
            var instance = Activator.CreateInstance(rowType)!;
            foreach (var column in schema)
            {
                var prop = rowType.GetProperty(column.NormalizedName);
                if (prop is null || !prop.CanWrite)
                {
                    continue;
                }

                row.TryGetValue(column.OriginalName, out var value);
                prop.SetValue(instance, ConvertValue(value, column.InferredType));
            }

            list.Add(instance);
        }

        return list;
    }

    private static object ConvertValue(string? value, string inferredType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return inferredType switch
            {
                "decimal" => 0m,
                "datetime" => DateTime.MinValue,
                "bool" => false,
                _ => string.Empty
            };
        }

        return inferredType switch
        {
            "decimal" when decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
            "datetime" when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) => dt,
            "bool" when bool.TryParse(value, out var b) => b,
            _ => value
        };
    }

    private static string BuildSource(IReadOnlyList<ColumnSchemaDto> schema)
    {
        var properties = schema.Select(column =>
            $"public {MapToClrType(column.InferredType)} {column.NormalizedName} {{ get; set; }}");

        return $$"""
using System;
namespace SpreadsheetFilterApp.Dynamic;
public sealed class Row
{
{{string.Join(Environment.NewLine, properties)}}
}
""";
    }

    private static string MapToClrType(string inferredType)
    {
        return inferredType switch
        {
            "decimal" => "decimal",
            "datetime" => "DateTime",
            "bool" => "bool",
            _ => "string"
        };
    }
}
