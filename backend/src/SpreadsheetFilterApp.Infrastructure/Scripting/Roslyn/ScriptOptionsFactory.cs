using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace SpreadsheetFilterApp.Infrastructure.Scripting.Roslyn;

public sealed class ScriptOptionsFactory
{
    public ScriptOptions Create(Assembly rowAssembly)
    {
        var references = new MetadataReference[]
        {
            CreateReference(typeof(object).Assembly),
            CreateReference(typeof(Enumerable).Assembly),
            CreateReference(typeof(Dictionary<,>).Assembly),
            CreateReference(typeof(CultureInfo).Assembly),
            CreateReference(typeof(SandboxGlobals<>).Assembly),
            CreateReference(rowAssembly)
        };

        return ScriptOptions.Default
            .WithReferences(references)
            .WithImports(
                "System",
                "System.Linq",
                "System.Collections.Generic",
                "System.Globalization");
    }

    private static MetadataReference CreateReference(Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly.Location))
        {
            throw new InvalidOperationException($"Could not load metadata reference for assembly '{assembly.FullName}' because it has no physical location.");
        }

        return MetadataReference.CreateFromFile(assembly.Location);
    }
}
