import { useEffect, useMemo, useRef, useState } from "react";
import { Download, FileSpreadsheet, History, Merge, Plus, RotateCcw, Upload, X } from "lucide-react";
import { Button } from "../../shared/components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../../shared/components/card";
import { Badge } from "../../shared/components/badge";
import { Toast } from "../../shared/components/toast";
import { Input } from "../../shared/components/input";
import {
  DialogBackdrop,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogPortal,
  DialogRoot,
  DialogTitle
} from "../../shared/components/dialog";
import {
  SelectItem,
  SelectPopup,
  SelectPortal,
  SelectPositioner,
  SelectRoot,
  SelectTrigger,
  SelectValue
} from "../../shared/components/select";
import { TabsList, TabsPanel, TabsRoot, TabsTrigger } from "../../shared/components/tabs";
import { LinqEditorPanel } from "../../shared/editor/linq-editor-panel";
import { useSandboxState } from "../../shared/hooks/use-sandbox-state";
import { formatElapsed, formatFileSize } from "../../shared/utils/format";
import { ColumnsViewer } from "../../shared/viewers/columns-viewer";
import { ResultViewer } from "../../shared/viewers/result-viewer";
import { SpreadsheetViewer } from "../../shared/viewers/spreadsheet-viewer";
import { useDownload } from "../../shared/hooks/use-download";
import { getQueryContract, unifySheets } from "../../shared/api/spreadsheets";
import { normalizeCompareText } from "../../shared/utils/name-normalization";

function acceptsFile(file: File): boolean {
  const lowerName = file.name.toLowerCase();
  return lowerName.endsWith(".csv") || lowerName.endsWith(".xlsx");
}

type FilterOperator = "equals" | "contains" | "startsWith" | "endsWith" | "isEmpty";

interface SimpleFilter {
  id: string;
  column: string;
  operator: FilterOperator;
  value: string;
  negate: boolean;
  joinWith: "AND" | "OR";
}

interface ProjectionField {
  id: string;
  column: string;
  alias: string;
}

function escapeString(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/\"/g, "\\\"");
}

function buildFilterExpression(filter: SimpleFilter): string {
  const col = escapeString(filter.column);
  const val = escapeString(filter.value);
  const norm = escapeString(normalizeCompareText(filter.value));

  let expr = "true";
  if (filter.operator === "equals") {
    expr = `r.Norm(\"${col}\") == \"${norm}\"`;
  } else if (filter.operator === "contains") {
    expr = `(r.Str(\"${col}\") ?? \"\").Contains(\"${val}\", StringComparison.OrdinalIgnoreCase)`;
  } else if (filter.operator === "startsWith") {
    expr = `(r.Str(\"${col}\") ?? \"\").StartsWith(\"${val}\", StringComparison.OrdinalIgnoreCase)`;
  } else if (filter.operator === "endsWith") {
    expr = `(r.Str(\"${col}\") ?? \"\").EndsWith(\"${val}\", StringComparison.OrdinalIgnoreCase)`;
  } else if (filter.operator === "isEmpty") {
    expr = `string.IsNullOrWhiteSpace(r.Str(\"${col}\"))`;
  }

  return filter.negate ? `!(${expr})` : expr;
}

function buildGeneratedCode(filters: SimpleFilter[], projections: ProjectionField[]): string {
  const activeFilters = filters.filter((f) => f.column);
  const condition = activeFilters.length === 0
    ? "true"
    : activeFilters
      .map((filter, index) => {
        const expr = buildFilterExpression(filter);
        if (index === 0) {
          return expr;
        }

        return `${filter.joinWith === "OR" ? "||" : "&&"} ${expr}`;
      })
      .join(" ");

  const activeProjections = projections.filter((p) => p.column);
  if (activeProjections.length === 0) {
    return `return rows.Where(r => ${condition}).ToList();`;
  }

  const projectionBody = activeProjections
    .map((field) => {
      const source = `r.Str(\"${escapeString(field.column)}\")`;
      const alias = field.alias.trim();
      const outputName = alias.length > 0 ? alias : field.column;
      return `["${escapeString(outputName)}"] = ${source}`;
    })
    .join(", ");

  return `return rows.Where(r => ${condition}).Select(r => new Dictionary<string, object?> { ${projectionBody} }).ToList();`;
}

export function SandboxPage() {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const inputRef2 = useRef<HTMLInputElement | null>(null);
  const inputRef3 = useRef<HTMLInputElement | null>(null);
  const downloadFile = useDownload();
  const { state, actions } = useSandboxState();
  const [downloadDialogOpen, setDownloadDialogOpen] = useState(false);
  const [unifyLoading, setUnifyLoading] = useState(false);

  const [primarySheet, setPrimarySheet] = useState("");
  const [primaryKeyColumn, setPrimaryKeyColumn] = useState("");
  const [comparisonColumns, setComparisonColumns] = useState<Record<string, string>>({});

  const [filters, setFilters] = useState<SimpleFilter[]>([{ id: crypto.randomUUID(), column: "", operator: "equals", value: "", negate: false, joinWith: "AND" }]);
  const [projections, setProjections] = useState<ProjectionField[]>([{ id: crypto.randomUUID(), column: "", alias: "" }]);

  const hasFile = Boolean(state.fileToken);

  const fileStatus = useMemo(() => {
    if (!state.file && !state.file2 && !state.file3) {
      return "No file";
    }

    const parts: string[] = [];
    if (state.file) {
      parts.push(`sheet1: ${state.file.name} (${formatFileSize(state.file.size)})`);
    }
    if (state.file2) {
      parts.push(`sheet2: ${state.file2.name} (${formatFileSize(state.file2.size)})`);
    }
    if (state.file3) {
      parts.push(`sheet3: ${state.file3.name} (${formatFileSize(state.file3.size)})`);
    }
    return parts.join(" | ");
  }, [state.file, state.file2, state.file3]);

  const primaryColumns = useMemo(() => {
    const sheet = state.sheets.find((x) => x.sheetName === primarySheet);
    return sheet?.columns ?? [];
  }, [state.sheets, primarySheet]);

  const childSheets = useMemo(() => {
    return state.sheets.filter((x) => x.sheetName !== primarySheet && x.sheetName !== "unified");
  }, [state.sheets, primarySheet]);

  useEffect(() => {
    if (state.sheets.length === 0) {
      return;
    }

    if (!primarySheet || !state.sheets.some((x) => x.sheetName === primarySheet)) {
      const first = state.sheets.find((x) => x.sheetName !== "unified") ?? state.sheets[0];
      setPrimarySheet(first?.sheetName ?? "");
    }
  }, [state.sheets, primarySheet]);

  useEffect(() => {
    if (!primaryColumns.includes(primaryKeyColumn)) {
      setPrimaryKeyColumn(primaryColumns[0] ?? "");
    }
  }, [primaryColumns, primaryKeyColumn]);

  const handleUpload = async (file?: File, file2?: File, file3?: File): Promise<void> => {
    if (!file) {
      return;
    }

    const selected = [file, file2, file3].filter((x): x is File => Boolean(x));
    if (!selected.every(acceptsFile)) {
      return;
    }

    await actions.uploadFile(file, file2, file3);
  };

  const handleRun = async (): Promise<void> => {
    const preview = await actions.runPreview();
    if (preview) {
      setDownloadDialogOpen(true);
    }
  };

  const handleConfirmDownload = async (): Promise<void> => {
    const result = await actions.downloadResult();
    if (result) {
      downloadFile(result.blob, result.fileName);
      setDownloadDialogOpen(false);
    }
  };

  const handleUnify = async (): Promise<void> => {
    if (!state.fileToken || !primarySheet || !primaryKeyColumn || childSheets.length === 0) {
      return;
    }

    const comparisons = childSheets
      .map((sheet) => ({
        sheetName: sheet.sheetName,
        compareColumn: comparisonColumns[sheet.sheetName] ?? ""
      }))
      .filter((x) => x.compareColumn);

    if (comparisons.length !== childSheets.length) {
      return;
    }

    setUnifyLoading(true);
    try {
      await unifySheets(state.fileToken, {
        primarySheetName: primarySheet,
        primaryKeyColumn,
        comparisons
      });

      const contract = await getQueryContract(state.fileToken);
      actions.applyContract(contract.sheets);
    } finally {
      setUnifyLoading(false);
    }
  };

  const addFilter = (): void => {
    setFilters((prev) => [...prev, { id: crypto.randomUUID(), column: state.columns[0]?.normalizedName ?? "", operator: "equals", value: "", negate: false, joinWith: "AND" }]);
  };

  const addProjection = (): void => {
    setProjections((prev) => [...prev, { id: crypto.randomUUID(), column: state.columns[0]?.normalizedName ?? "", alias: "" }]);
  };

  const applyGeneratedCode = (): void => {
    const nextCode = buildGeneratedCode(filters, projections);
    actions.setCode(nextCode);
  };

  return (
    <div data-slot="sandbox-page" className="min-h-screen bg-surface pb-8">
      <header className="sticky top-0 z-20 border-b border-border bg-surface/95 backdrop-blur">
        <div className="mx-auto flex w-full max-w-[1400px] flex-wrap items-center gap-3 px-4 py-3">
          <div className="flex items-center gap-2">
            <div className="rounded-xl border border-border bg-surface-raised p-2">
              <FileSpreadsheet className="h-5 w-5 text-primary" />
            </div>
            <div>
              <h1 className="text-sm font-semibold sm:text-base">LINQ Spreadsheet Sandbox</h1>
              <p className="text-xs text-muted-foreground">{fileStatus}</p>
            </div>
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            <input
              ref={inputRef}
              type="file"
              className="hidden"
              accept=".csv,.xlsx"
              onChange={(event) => void handleUpload(event.target.files?.[0], state.file2, state.file3)}
            />
            <input
              ref={inputRef2}
              type="file"
              className="hidden"
              accept=".csv,.xlsx"
              onChange={(event) => void handleUpload(state.file, event.target.files?.[0], state.file3)}
            />
            <input
              ref={inputRef3}
              type="file"
              className="hidden"
              accept=".csv,.xlsx"
              onChange={(event) => void handleUpload(state.file, state.file2, event.target.files?.[0])}
            />
            <Button variant="secondary" onClick={() => inputRef.current?.click()} aria-label="Upload file">
              <Upload className="h-4 w-4" />
              Upload 1
            </Button>
            <Button variant="secondary" onClick={() => inputRef2.current?.click()} aria-label="Upload second file">
              <Upload className="h-4 w-4" />
              Upload 2
            </Button>
            <Button variant="secondary" onClick={() => inputRef3.current?.click()} aria-label="Upload third file">
              <Upload className="h-4 w-4" />
              Upload 3
            </Button>
            <Button variant="ghost" onClick={actions.reset} aria-label="Reset sandbox">
              <RotateCcw className="h-4 w-4" />
              Reset
            </Button>

            <SelectRoot value={state.outputFormat} onValueChange={(value) => actions.setOutputFormat(value as "csv" | "xlsx")}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectPortal>
                <SelectPositioner>
                  <SelectPopup>
                    <SelectItem value="csv">CSV</SelectItem>
                    <SelectItem value="xlsx">XLSX</SelectItem>
                  </SelectPopup>
                </SelectPositioner>
              </SelectPortal>
            </SelectRoot>
          </div>
        </div>
      </header>

      <main className="mx-auto grid w-full max-w-[1400px] gap-4 px-4 pt-4 lg:grid-cols-[minmax(0,62fr)_minmax(0,38fr)]">
        <section className="min-h-0">
          {!hasFile ? (
            <Card className="h-[70vh]">
              <CardContent className="flex h-full flex-col items-center justify-center gap-4 text-center">
                <div className="rounded-full border border-border bg-muted p-4">
                  <Upload className="h-8 w-8 text-primary" />
                </div>
                <div>
                  <h2 className="text-lg font-semibold">Upload ate 3 planilhas CSV/XLSX</h2>
                  <p className="mt-1 text-sm text-muted-foreground">Depois escolha a planilha mae, chave primaria e mapeie as colunas de comparacao.</p>
                </div>
                <Button onClick={() => inputRef.current?.click()}>
                  <Upload className="h-4 w-4" />
                  Escolher planilha 1
                </Button>
              </CardContent>
            </Card>
          ) : (
            <TabsRoot defaultValue="spreadsheet" className="h-full">
              <TabsList>
                <TabsTrigger value="spreadsheet">Planilha</TabsTrigger>
                <TabsTrigger value="result">Resultado</TabsTrigger>
                <TabsTrigger value="columns">Colunas</TabsTrigger>
              </TabsList>
              <TabsPanel value="spreadsheet">
                <SpreadsheetViewer preview={state.originalPreview} sheets={state.sheets} />
              </TabsPanel>
              <TabsPanel value="result">
                <ResultViewer
                  preview={state.resultPreview}
                  canDownload={Boolean(state.fileToken && state.resultPreview)}
                  onDownload={() => setDownloadDialogOpen(true)}
                />
              </TabsPanel>
              <TabsPanel value="columns">
                <ColumnsViewer columns={state.columns} />
              </TabsPanel>
            </TabsRoot>
          )}
        </section>

        <section className="space-y-4">
          {state.schemaLoading ? (
            <Card>
              <CardHeader>
                <CardTitle>Processing upload</CardTitle>
                <Badge variant="info">{state.uploadProgress}%</Badge>
              </CardHeader>
              <CardContent>
                <p className="mb-2 text-sm text-muted-foreground">
                  {state.uploadStage ?? "Queued"} {state.uploadMessage ? `- ${state.uploadMessage}` : ""}
                </p>
                <div className="h-2 w-full rounded bg-muted">
                  <div
                    className="h-2 rounded bg-primary transition-all duration-300"
                    style={{ width: `${Math.min(100, Math.max(0, state.uploadProgress))}%` }}
                  />
                </div>
              </CardContent>
            </Card>
          ) : null}

          {state.errorMessage ? <Toast tone="error">{state.errorMessage}</Toast> : null}

          <Card>
            <CardHeader>
              <CardTitle>Unificar planilhas</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid gap-2 sm:grid-cols-2">
                <SelectRoot value={primarySheet} onValueChange={setPrimarySheet}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectPortal><SelectPositioner><SelectPopup>
                    {state.sheets.filter((x) => x.sheetName !== "unified").map((sheet) => (
                      <SelectItem key={sheet.sheetName} value={sheet.sheetName}>{sheet.sheetName}</SelectItem>
                    ))}
                  </SelectPopup></SelectPositioner></SelectPortal>
                </SelectRoot>
                <SelectRoot value={primaryKeyColumn} onValueChange={setPrimaryKeyColumn}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectPortal><SelectPositioner><SelectPopup>
                    {primaryColumns.map((col) => (
                      <SelectItem key={col} value={col}>{col}</SelectItem>
                    ))}
                  </SelectPopup></SelectPositioner></SelectPortal>
                </SelectRoot>
              </div>

              {childSheets.map((sheet) => (
                <div key={sheet.sheetName} className="grid gap-2 sm:grid-cols-2">
                  <div className="rounded-xl border border-border bg-muted px-3 py-2 text-sm">{sheet.sheetName}</div>
                  <SelectRoot
                    value={comparisonColumns[sheet.sheetName] ?? ""}
                    onValueChange={(value) => setComparisonColumns((prev) => ({ ...prev, [sheet.sheetName]: value }))}
                  >
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectPortal><SelectPositioner><SelectPopup>
                      {sheet.columns.map((col) => (
                        <SelectItem key={`${sheet.sheetName}-${col}`} value={col}>{col}</SelectItem>
                      ))}
                    </SelectPopup></SelectPositioner></SelectPortal>
                  </SelectRoot>
                </div>
              ))}

              <Button onClick={() => void handleUnify()} disabled={unifyLoading || !state.fileToken || childSheets.length === 0}>
                <Merge className="h-4 w-4" />
                {unifyLoading ? "Unificando..." : "Unificar por chave primaria"}
              </Button>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Filtros simples e projection</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              {filters.map((filter, index) => (
                <div key={filter.id} className="space-y-2 rounded-xl border border-border p-3">
                  <div className="grid gap-2 sm:grid-cols-2">
                    <SelectRoot value={filter.column} onValueChange={(value) => setFilters((prev) => prev.map((x) => x.id === filter.id ? { ...x, column: value } : x))}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectPortal><SelectPositioner><SelectPopup>
                        {state.columns.map((column) => (
                          <SelectItem key={column.normalizedName} value={column.normalizedName}>{column.normalizedName}</SelectItem>
                        ))}
                      </SelectPopup></SelectPositioner></SelectPortal>
                    </SelectRoot>
                    <SelectRoot value={filter.operator} onValueChange={(value) => setFilters((prev) => prev.map((x) => x.id === filter.id ? { ...x, operator: value as FilterOperator } : x))}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectPortal><SelectPositioner><SelectPopup>
                        <SelectItem value="equals">Igual (normalizado)</SelectItem>
                        <SelectItem value="contains">Contem</SelectItem>
                        <SelectItem value="startsWith">Comeca com</SelectItem>
                        <SelectItem value="endsWith">Termina com</SelectItem>
                        <SelectItem value="isEmpty">Vazio</SelectItem>
                      </SelectPopup></SelectPositioner></SelectPortal>
                    </SelectRoot>
                  </div>

                  <div className="grid gap-2 sm:grid-cols-[1fr_auto_auto_auto]">
                    <Input
                      value={filter.value}
                      onChange={(event) => setFilters((prev) => prev.map((x) => x.id === filter.id ? { ...x, value: event.target.value } : x))}
                      placeholder="Valor esperado"
                      disabled={filter.operator === "isEmpty"}
                    />
                    <Button variant={filter.negate ? "primary" : "secondary"} onClick={() => setFilters((prev) => prev.map((x) => x.id === filter.id ? { ...x, negate: !x.negate } : x))}>NOT</Button>
                    {index > 0 ? (
                      <SelectRoot value={filter.joinWith} onValueChange={(value) => setFilters((prev) => prev.map((x) => x.id === filter.id ? { ...x, joinWith: value as "AND" | "OR" } : x))}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectPortal><SelectPositioner><SelectPopup>
                          <SelectItem value="AND">AND</SelectItem>
                          <SelectItem value="OR">OR</SelectItem>
                        </SelectPopup></SelectPositioner></SelectPortal>
                      </SelectRoot>
                    ) : <div />}
                    <Button variant="ghost" onClick={() => setFilters((prev) => prev.length > 1 ? prev.filter((x) => x.id !== filter.id) : prev)}>
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}

              <Button variant="secondary" onClick={addFilter}><Plus className="h-4 w-4" />Adicionar filtro</Button>

              <div className="space-y-2 rounded-xl border border-border p-3">
                <h3 className="text-sm font-semibold">Projection</h3>
                {projections.map((projection) => (
                  <div key={projection.id} className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]">
                    <Input
                      value={projection.alias}
                      onChange={(event) => setProjections((prev) => prev.map((x) => x.id === projection.id ? { ...x, alias: event.target.value } : x))}
                      placeholder="Novo nome (opcional)"
                    />
                    <SelectRoot value={projection.column} onValueChange={(value) => setProjections((prev) => prev.map((x) => x.id === projection.id ? { ...x, column: value } : x))}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectPortal><SelectPositioner><SelectPopup>
                        {state.columns.map((column) => (
                          <SelectItem key={`proj-${column.normalizedName}`} value={column.normalizedName}>{column.normalizedName}</SelectItem>
                        ))}
                      </SelectPopup></SelectPositioner></SelectPortal>
                    </SelectRoot>
                    <Button variant="ghost" onClick={() => setProjections((prev) => prev.length > 1 ? prev.filter((x) => x.id !== projection.id) : prev)}>
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                ))}
                <Button variant="secondary" onClick={addProjection}><Plus className="h-4 w-4" />Adicionar coluna</Button>
              </div>

              <Button onClick={applyGeneratedCode}>Gerar LINQ pelos filtros</Button>
            </CardContent>
          </Card>

          <LinqEditorPanel
            code={state.linqCode}
            columns={state.columns}
            sheets={state.sheets}
            diagnostics={state.diagnostics}
            validateLoading={state.validateLoading}
            runLoading={state.runLoading}
            onCodeChange={actions.setCode}
            onValidate={actions.validate}
            onRun={handleRun}
          />

          <Card>
            <CardHeader>
              <CardTitle>Execution history</CardTitle>
              <Badge variant="info">{state.runHistory.length}</Badge>
            </CardHeader>
            <CardContent>
              {state.runHistory.length === 0 ? (
                <p className="text-sm text-muted-foreground">No runs yet.</p>
              ) : (
                <ul className="space-y-2">
                  {state.runHistory.map((entry) => (
                    <li key={entry.at} className="flex items-center justify-between rounded-xl border border-border bg-surface p-2 text-sm">
                      <span className="inline-flex items-center gap-2 text-foreground-subtle">
                        <History className="h-4 w-4" />
                        {new Date(entry.at).toLocaleTimeString()} · {entry.rowCountPreview} rows
                      </span>
                      <span className="text-xs text-muted-foreground">
                        {formatElapsed(entry.elapsedMs)} · {entry.diagnostics} diagnostics
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </section>
      </main>

      <DialogRoot open={downloadDialogOpen} onOpenChange={setDownloadDialogOpen}>
        <DialogPortal>
          <DialogBackdrop />
          <DialogContent>
            <DialogTitle>Preview concluido</DialogTitle>
            <DialogDescription>
              O preview retornou {state.resultPreview?.rowCountPreview ?? 0} linhas. Deseja baixar o arquivo final em {state.outputFormat.toUpperCase()}?
            </DialogDescription>
            <div className="mt-4 flex justify-end gap-2">
              <DialogClose>
                <Button variant="ghost">Cancelar</Button>
              </DialogClose>
              <Button onClick={() => void handleConfirmDownload()} disabled={state.downloadLoading}>
                <Download className="h-4 w-4" />
                {state.downloadLoading ? "Baixando..." : "Baixar"}
              </Button>
            </div>
          </DialogContent>
        </DialogPortal>
      </DialogRoot>
    </div>
  );
}

