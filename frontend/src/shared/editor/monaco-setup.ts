import type * as Monaco from "monaco-editor";

export function setupMonaco(monaco: typeof Monaco): void {
  monaco.languages.typescript.javascriptDefaults.setDiagnosticsOptions({
    noSemanticValidation: false,
    noSyntaxValidation: false
  });

  monaco.languages.register({ id: "csharp" });
}
