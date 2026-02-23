import type * as Monaco from "monaco-editor";

export interface LinqSnippet {
  label: string;
  detail: string;
  insertText: string;
}

export const LINQ_SNIPPETS: LinqSnippet[] = [
  {
    label: "Where",
    detail: "Filter rows",
    insertText: "rows.Where(row => row.${1:Column} == ${2:\"value\"})"
  },
  {
    label: "OrderBy",
    detail: "Order rows",
    insertText: "rows.OrderBy(row => row.${1:Column})"
  },
  {
    label: "Select",
    detail: "Project columns",
    insertText: "rows.Select(row => new { ${1:row.Column} })"
  },
  {
    label: "GroupBy Count",
    detail: "Group and count",
    insertText: "rows.GroupBy(row => row.${1:Column}).Select(g => new { Key = g.Key, Total = g.Count() })"
  }
];

export function toCompletionItem(
  monaco: typeof Monaco,
  snippet: LinqSnippet,
  range: Monaco.IRange
): Monaco.languages.CompletionItem {
  return {
    label: snippet.label,
    kind: monaco.languages.CompletionItemKind.Snippet,
    documentation: snippet.detail,
    insertText: snippet.insertText,
    insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
    range
  };
}
