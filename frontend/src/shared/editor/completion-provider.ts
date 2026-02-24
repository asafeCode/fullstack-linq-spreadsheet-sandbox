import type * as Monaco from "monaco-editor";
import type { QueryContractSheet, SpreadsheetColumn } from "../api/types";

function linePrefix(model: Monaco.editor.ITextModel, position: Monaco.Position): string {
  return model.getLineContent(position.lineNumber).slice(0, position.column - 1);
}

export function createCompletionProvider(
  monaco: typeof Monaco,
  columns: SpreadsheetColumn[],
  _sheets: QueryContractSheet[] = []
): Monaco.IDisposable {
  return monaco.languages.registerCompletionItemProvider("csharp", {
    triggerCharacters: ["."],
    provideCompletionItems(model, position) {
      const word = model.getWordUntilPosition(position);
      const range = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        endColumn: word.endColumn
      };

      const prefix = linePrefix(model, position);
      const afterColumnDot = /\b[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*\.$/.test(prefix);
      const afterRowDot = /\b[A-Za-z_][A-Za-z0-9_]*\.$/.test(prefix);

      if (afterColumnDot) {
        return {
          suggestions: [
            {
              label: "Normalize()",
              kind: monaco.languages.CompletionItemKind.Method,
              detail: "String normalization",
              insertText: "Normalize()",
              range
            }
          ]
        };
      }

      if (afterRowDot) {
        const uniqueColumns = Array.from(
          new Map(columns.map((column) => [column.normalizedName.toLowerCase(), column])).values()
        );

        return {
          suggestions: uniqueColumns.map((column) => ({
            label: column.normalizedName,
            kind: monaco.languages.CompletionItemKind.Field,
            detail: "JSON column",
            documentation: `${column.originalName} (${column.inferredType})`,
            insertText: column.normalizedName,
            range
          }))
        };
      }

      return { suggestions: [] };
    }
  });
}
