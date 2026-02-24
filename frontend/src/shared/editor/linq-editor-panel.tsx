import Editor, { type Monaco } from "@monaco-editor/react";
import type * as MonacoCore from "monaco-editor";
import { useEffect, useMemo, useRef, useState } from "react";
import { Play, WandSparkles, CheckCheck, ListFilter } from "lucide-react";
import type { LintDiagnostic, QueryContractSheet, SpreadsheetColumn } from "../api/types";
import { Badge } from "../components/badge";
import { Button } from "../components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../components/card";
import { Input } from "../components/input";
import { MenuItem, MenuPopup, MenuPortal, MenuPositioner, MenuRoot, MenuTrigger } from "../components/menu";
import {
  SelectItem,
  SelectPopup,
  SelectPortal,
  SelectPositioner,
  SelectRoot,
  SelectTrigger,
  SelectValue
} from "../components/select";
import { mapDiagnosticsToMarkers } from "./diagnostics-mapper";
import { createCompletionProvider } from "./completion-provider";
import { LINQ_SNIPPETS } from "./linq-snippets";
import { setupMonaco } from "./monaco-setup";
import { normalizeCompareText } from "../utils/name-normalization";

export interface LinqEditorPanelProps {
  code: string;
  columns: SpreadsheetColumn[];
  sheets?: QueryContractSheet[];
  diagnostics: LintDiagnostic[];
  validateLoading: boolean;
  runLoading: boolean;
  onCodeChange: (next: string) => void;
  onValidate: () => Promise<void>;
  onRun: () => Promise<void>;
}

function diagnosticsTone(severity: LintDiagnostic["severity"]): "error" | "warn" | "info" {
  if (severity === "error") {
    return "error";
  }

  if (severity === "warning") {
    return "warn";
  }

  return "info";
}

export function LinqEditorPanel({
  code,
  columns,
  sheets = [],
  diagnostics,
  validateLoading,
  runLoading,
  onCodeChange,
  onValidate,
  onRun
}: LinqEditorPanelProps) {
  const [columnSearch, setColumnSearch] = useState("");
  const monacoRef = useRef<MonacoCore.editor.IStandaloneCodeEditor | null>(null);
  const monacoInstanceRef = useRef<Monaco | null>(null);
  const completionDisposableRef = useRef<MonacoCore.IDisposable | null>(null);

  const filteredColumns = useMemo(() => {
    const term = normalizeCompareText(columnSearch);
    if (!term) {
      return columns;
    }

    return columns.filter((column) =>
      normalizeCompareText([column.originalName, column.normalizedName, column.inferredType].join(" ")).includes(term)
    );
  }, [columns, columnSearch]);

  const applyMarkers = (): void => {
    const monaco = monacoInstanceRef.current;
    const editor = monacoRef.current;
    if (!monaco || !editor) {
      return;
    }

    const model = editor.getModel();
    if (!model) {
      return;
    }

    monaco.editor.setModelMarkers(model, "linq-diagnostics", mapDiagnosticsToMarkers(diagnostics));
  };

  const refreshCompletionProvider = (): void => {
    const monaco = monacoInstanceRef.current;
    if (!monaco) {
      return;
    }

    completionDisposableRef.current?.dispose();
    completionDisposableRef.current = createCompletionProvider(monaco, columns, sheets);
  };

  useEffect(() => {
    applyMarkers();
  }, [diagnostics]);

  useEffect(() => {
    refreshCompletionProvider();

    return () => {
      completionDisposableRef.current?.dispose();
    };
  }, [columns, sheets]);

  const insertSnippet = (text: string): void => {
    const editor = monacoRef.current;
    if (!editor) {
      onCodeChange(`${code}\n${text}`);
      return;
    }

    const selection = editor.getSelection();
    if (!selection) {
      return;
    }

    editor.executeEdits("snippet", [{ range: selection, text }]);
    const next = editor.getValue();
    onCodeChange(next);
  };

  const jumpToDiagnostic = (line: number, column: number): void => {
    const editor = monacoRef.current;
    if (!editor) {
      return;
    }

    editor.focus();
    editor.revealLineInCenter(line);
    editor.setPosition({ lineNumber: line, column });
  };

  return (
    <Card data-slot="linq-editor-panel" className="h-full">
      <CardHeader className="pb-3">
        <div>
          <CardTitle>LINQ editor</CardTitle>
          <p className="mt-1 text-xs text-muted-foreground">Write queries against <code>rows</code>.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => void onValidate()} disabled={validateLoading || runLoading}>
            <CheckCheck className="h-4 w-4" />
            Validate
          </Button>
          <Button size="sm" onClick={() => void onRun()} disabled={runLoading}>
            <Play className="h-4 w-4" />
            {runLoading ? "Running" : "Run"}
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <pre data-slot="linq-code-value" className="sr-only">{code}</pre>
        <div className="rounded-xl border border-border">
          <Editor
            height="340px"
            defaultLanguage="csharp"
            value={code}
            onChange={(value) => onCodeChange(value ?? "")}
            beforeMount={(monaco) => {
              setupMonaco(monaco);
            }}
            onMount={(editor, monaco) => {
              monacoRef.current = editor;
              monacoInstanceRef.current = monaco;
              refreshCompletionProvider();
              applyMarkers();
            }}
            options={{
              minimap: { enabled: false },
              fontSize: 13,
              scrollBeyondLastLine: false,
              lineNumbersMinChars: 3,
              tabSize: 2,
              padding: { top: 10, bottom: 10 }
            }}
          />
        </div>

        <div className="grid gap-2 sm:grid-cols-[1fr_auto_auto]">
          <Input value={columnSearch} onChange={(event) => setColumnSearch(event.target.value)} placeholder="Search column to insert" />
          <SelectRoot defaultValue={filteredColumns[0]?.normalizedName ?? ""}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectPortal>
              <SelectPositioner>
                <SelectPopup>
                  {filteredColumns.map((column) => (
                    <SelectItem key={column.normalizedName} value={column.normalizedName} onClick={() => insertSnippet(`row.${column.normalizedName}`)}>
                      {column.normalizedName}
                    </SelectItem>
                  ))}
                </SelectPopup>
              </SelectPositioner>
            </SelectPortal>
          </SelectRoot>
          <MenuRoot>
            <MenuTrigger><Button variant="secondary" size="sm"><ListFilter className="h-4 w-4" />Snippets</Button></MenuTrigger>
            <MenuPortal>
              <MenuPositioner>
                <MenuPopup>
                  {LINQ_SNIPPETS.map((snippet) => (
                    <MenuItem key={snippet.label} onClick={() => insertSnippet(snippet.insertText)}>
                      {snippet.label}
                    </MenuItem>
                  ))}
                  <MenuItem onClick={() => onCodeChange(code.trim())}>
                    <WandSparkles className="mr-2 inline h-4 w-4" />
                    Format
                  </MenuItem>
                </MenuPopup>
              </MenuPositioner>
            </MenuPortal>
          </MenuRoot>
        </div>

        <div className="space-y-2 rounded-xl border border-border bg-surface p-3">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Diagnostics</h3>
          {diagnostics.length === 0 ? (
            <p className="text-sm text-muted-foreground">No diagnostics.</p>
          ) : (
            <ul className="space-y-2">
              {diagnostics.map((diagnostic, index) => (
                <li key={`${index}-${diagnostic.message}`}>
                  <button
                    data-slot="diagnostic-item"
                    type="button"
                    className="w-full rounded-lg border border-border bg-surface-raised p-2 text-left hover:bg-muted"
                    onClick={() => jumpToDiagnostic(diagnostic.line, diagnostic.column)}
                  >
                    <div className="mb-1 flex items-center gap-2">
                      <Badge variant={diagnosticsTone(diagnostic.severity)}>{diagnostic.severity}</Badge>
                      <span className="text-xs text-muted-foreground">
                        L{diagnostic.line}:C{diagnostic.column}
                      </span>
                    </div>
                    <p className="text-sm text-foreground-subtle">{diagnostic.message}</p>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
