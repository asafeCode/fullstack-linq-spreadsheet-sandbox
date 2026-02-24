import { useEffect, useMemo, useRef, useState } from "react";
import { nanoid } from "nanoid";
import { Download } from "lucide-react";
import { tv } from "tailwind-variants";
import { twMerge } from "tailwind-merge";
import { Button } from "../../shared/components/button";
import {
  DialogBackdrop,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogPortal,
  DialogRoot,
  DialogTitle
} from "../../shared/components/dialog";
import { Toast } from "../../shared/components/toast";
import { TabsList, TabsPanel, TabsRoot, TabsTrigger } from "../../shared/components/tabs";
import { LinqEditorPanel } from "../../shared/editor/linq-editor-panel";
import { useSandboxState } from "../../shared/hooks/use-sandbox-state";
import { formatFileSize } from "../../shared/utils/format";
import { ColumnsViewer } from "../../shared/viewers/columns-viewer";
import { ResultViewer } from "../../shared/viewers/result-viewer";
import { SpreadsheetViewer } from "../../shared/viewers/spreadsheet-viewer";
import { useDownload } from "../../shared/hooks/use-download";
import { getQueryContract, unifySheets } from "../../shared/api/spreadsheets";
import { normalizeCompareText } from "../../shared/utils/name-normalization";
import { createSavedQuery, deleteSavedQuery, getSavedQueryById, listSavedQueries } from "../../shared/api/saved-queries";
import type { SavedQuery } from "../../shared/api/types";
import { EmptyUploadState } from "./sandbox-page-components/empty-upload-state";
import { ExecutionHistoryCard } from "./sandbox-page-components/execution-history-card";
import { FilterBuilderCard } from "./sandbox-page-components/filter-builder-card";
import type { ProjectionField, SimpleFilter } from "./sandbox-page-components/sandbox-builder.types";
import { SandboxPageHeader } from "./sandbox-page-components/sandbox-page-header";
import { SavedQueriesCard } from "./sandbox-page-components/saved-queries-card";
import { UnifySheetsCard } from "./sandbox-page-components/unify-sheets-card";
import { UploadProgressCard } from "./sandbox-page-components/upload-progress-card";

function acceptsFile(file: File): boolean {
  const lowerName = file.name.toLowerCase();
  return lowerName.endsWith(".csv") || lowerName.endsWith(".xlsx");
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
      return `[\"${escapeString(outputName)}\"] = ${source}`;
    })
    .join(", ");

  return `return rows.Where(r => ${condition}).Select(r => new Dictionary<string, object?> { ${projectionBody} }).ToList();`;
}

const sandboxPageVariants = tv({
  slots: {
    root: "min-h-screen bg-surface",
    main: "mx-auto grid w-full max-w-[1400px] gap-3 px-3 py-3 lg:h-[calc(100vh-4rem)] lg:grid-cols-[minmax(0,62fr)_minmax(0,38fr)]",
    left: "min-h-0",
    right: "min-h-0"
  }
});

export function SandboxPage() {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const inputRef2 = useRef<HTMLInputElement | null>(null);
  const inputRef3 = useRef<HTMLInputElement | null>(null);
  const downloadFile = useDownload();
  const { state, actions } = useSandboxState();
  const [downloadDialogOpen, setDownloadDialogOpen] = useState(false);
  const [editorDialogOpen, setEditorDialogOpen] = useState(false);
  const [historyDialogOpen, setHistoryDialogOpen] = useState(false);
  const [unifyLoading, setUnifyLoading] = useState(false);

  const [primarySheet, setPrimarySheet] = useState("");
  const [primaryKeyColumn, setPrimaryKeyColumn] = useState("");
  const [comparisonColumns, setComparisonColumns] = useState<Record<string, string>>({});

  const [filters, setFilters] = useState<SimpleFilter[]>([{ id: nanoid(), column: "", operator: "equals", value: "", negate: false, joinWith: "AND" }]);
  const [projections, setProjections] = useState<ProjectionField[]>([{ id: nanoid(), column: "", alias: "" }]);
  const [savedQueries, setSavedQueries] = useState<SavedQuery[]>([]);
  const [savedQueryName, setSavedQueryName] = useState("");
  const [selectedSavedQueryId, setSelectedSavedQueryId] = useState<number | null>(null);
  const [savedQueriesLoading, setSavedQueriesLoading] = useState(false);
  const [savedQueriesBusy, setSavedQueriesBusy] = useState(false);
  const [savedQueriesMessage, setSavedQueriesMessage] = useState<{ tone: "info" | "error"; text: string } | null>(null);
  const [generatedCodePreview, setGeneratedCodePreview] = useState<string>("");

  const hasFile = Boolean(state.fileToken);
  const styles = sandboxPageVariants();

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

  useEffect(() => {
    const load = async (): Promise<void> => {
      setSavedQueriesLoading(true);
      try {
        const items = await listSavedQueries();
        setSavedQueries(items);
      } catch {
        setSavedQueriesMessage({ tone: "error", text: "Falha ao carregar queries salvas." });
      } finally {
        setSavedQueriesLoading(false);
      }
    };

    void load();
  }, []);

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

  const handleSaveCurrentQuery = async (): Promise<void> => {
    const name = savedQueryName.trim();
    if (name.length === 0) {
      setSavedQueriesMessage({ tone: "error", text: "Informe um nome para salvar a query." });
      return;
    }

    setSavedQueriesBusy(true);
    try {
      const created = await createSavedQuery({
        name,
        linqCode: state.linqCode
      });

      setSavedQueries((prev) => [created, ...prev.filter((x) => x.id !== created.id)]);
      setSelectedSavedQueryId(created.id);
      setSavedQueryName("");
      setSavedQueriesMessage({ tone: "info", text: "Query salva com sucesso." });
    } catch {
      setSavedQueriesMessage({ tone: "error", text: "Nao foi possivel salvar a query." });
    } finally {
      setSavedQueriesBusy(false);
    }
  };

  const handleLoadSavedQuery = async (id: number): Promise<void> => {
    setSavedQueriesBusy(true);
    try {
      const loaded = await getSavedQueryById(id);
      actions.setCode(loaded.linqCode);
      setSelectedSavedQueryId(loaded.id);
      setSavedQueries((prev) => prev.map((item) => item.id === loaded.id ? loaded : item));
      setSavedQueriesMessage({ tone: "info", text: `Query \"${loaded.name}\" carregada.` });
    } catch {
      setSavedQueriesMessage({ tone: "error", text: "Nao foi possivel carregar a query selecionada." });
    } finally {
      setSavedQueriesBusy(false);
    }
  };

  const handleDeleteSavedQuery = async (id: number): Promise<void> => {
    setSavedQueriesBusy(true);
    try {
      await deleteSavedQuery(id);
      setSavedQueries((prev) => prev.filter((item) => item.id !== id));
      if (selectedSavedQueryId === id) {
        setSelectedSavedQueryId(null);
      }
      setSavedQueriesMessage({ tone: "info", text: "Query excluida." });
    } catch {
      setSavedQueriesMessage({ tone: "error", text: "Falha ao excluir query." });
    } finally {
      setSavedQueriesBusy(false);
    }
  };

  return (
    <div data-slot="sandbox-page" className={styles.root()}>
      <SandboxPageHeader
        fileStatus={fileStatus}
        inputRef1={inputRef}
        inputRef2={inputRef2}
        inputRef3={inputRef3}
        onSelectFile1={(file) => void handleUpload(file, state.file2, state.file3)}
        onSelectFile2={(file) => void handleUpload(state.file, file, state.file3)}
        onSelectFile3={(file) => void handleUpload(state.file, state.file2, file)}
        onReset={actions.reset}
        runHistoryCount={state.runHistory.length}
        onOpenHistory={() => setHistoryDialogOpen(true)}
      />

      <main className={styles.main()}>
        <section className={styles.left()}>
          {!hasFile ? (
            <EmptyUploadState onPickPrimaryFile={() => inputRef.current?.click()} />
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

        <section className={twMerge(styles.right(), "space-y-4")}>
          {state.schemaLoading ? (
            <UploadProgressCard
              progress={state.uploadProgress}
              stage={state.uploadStage}
              message={state.uploadMessage}
            />
          ) : null}

          {state.errorMessage ? <Toast tone="error">{state.errorMessage}</Toast> : null}
          {savedQueriesMessage ? <Toast tone={savedQueriesMessage.tone}>{savedQueriesMessage.text}</Toast> : null}

          <UnifySheetsCard
            sheets={state.sheets}
            primaryColumns={primaryColumns}
            childSheets={childSheets}
            primarySheet={primarySheet}
            primaryKeyColumn={primaryKeyColumn}
            comparisonColumns={comparisonColumns}
            unifyLoading={unifyLoading}
            hasFileToken={Boolean(state.fileToken)}
            onPrimarySheetChange={setPrimarySheet}
            onPrimaryKeyColumnChange={setPrimaryKeyColumn}
            onComparisonColumnChange={(sheetName, value) => setComparisonColumns((prev) => ({ ...prev, [sheetName]: value }))}
            onUnify={() => void handleUnify()}
          />

          <SavedQueriesCard
            savedQueries={savedQueries}
            savedQueryName={savedQueryName}
            selectedSavedQueryId={selectedSavedQueryId}
            loading={savedQueriesLoading}
            busy={savedQueriesBusy}
            onSavedQueryNameChange={setSavedQueryName}
            onSaveCurrentQuery={() => void handleSaveCurrentQuery()}
            onLoadSavedQuery={(id) => void handleLoadSavedQuery(id)}
            onDeleteSavedQuery={(id) => void handleDeleteSavedQuery(id)}
          />

          <FilterBuilderCard
            columns={state.columns}
            filters={filters}
            projections={projections}
            generatedCode={generatedCodePreview}
            runLoading={state.runLoading}
            onFilterChange={(id, patch) => setFilters((prev) => prev.map((item) => item.id === id ? { ...item, ...patch } : item))}
            onRemoveFilter={(id) => setFilters((prev) => prev.length > 1 ? prev.filter((item) => item.id !== id) : prev)}
            onAddFilter={() => setFilters((prev) => [...prev, { id: nanoid(), column: state.columns[0]?.normalizedName ?? "", operator: "equals", value: "", negate: false, joinWith: "AND" }])}
            onProjectionChange={(id, patch) => setProjections((prev) => prev.map((item) => item.id === id ? { ...item, ...patch } : item))}
            onRemoveProjection={(id) => setProjections((prev) => prev.length > 1 ? prev.filter((item) => item.id !== id) : prev)}
            onAddProjection={() => setProjections((prev) => [...prev, { id: nanoid(), column: state.columns[0]?.normalizedName ?? "", alias: "" }])}
            onApplyGeneratedCode={() => {
              const generated = buildGeneratedCode(filters, projections);
              setGeneratedCodePreview(generated);
              actions.setCode(generated);
            }}
            onOpenCodeEditor={() => setEditorDialogOpen(true)}
            onRunQuery={() => void handleRun()}
          />
        </section>
      </main>

      <DialogRoot open={editorDialogOpen} onOpenChange={setEditorDialogOpen}>
        <DialogPortal>
          <DialogBackdrop />
          <DialogContent className="w-[min(96vw,1100px)] p-0">
            <div className="border-b border-border px-5 py-4">
              <DialogTitle>Editor LINQ</DialogTitle>
              <DialogDescription>
                Escreva e valide sua consulta antes de executar.
              </DialogDescription>
            </div>
            <div className="h-[min(80vh,760px)] p-4">
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
            </div>
          </DialogContent>
        </DialogPortal>
      </DialogRoot>

      <DialogRoot open={historyDialogOpen} onOpenChange={setHistoryDialogOpen}>
        <DialogPortal>
          <DialogBackdrop />
          <DialogContent className="w-[min(92vw,760px)]">
            <DialogTitle>Execution history</DialogTitle>
            <DialogDescription>Historico das execucoes recentes.</DialogDescription>
            <div className="mt-4 h-[min(70vh,560px)]">
              <ExecutionHistoryCard runHistory={state.runHistory} />
            </div>
          </DialogContent>
        </DialogPortal>
      </DialogRoot>

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
                <Download className="size-4" />
                {state.downloadLoading ? "Baixando..." : "Baixar"}
              </Button>
            </div>
          </DialogContent>
        </DialogPortal>
      </DialogRoot>
    </div>
  );
}




