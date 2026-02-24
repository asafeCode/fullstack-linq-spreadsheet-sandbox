import { useCallback, useReducer } from "react";
import {
  createUploadJob,
  getPreviewPage,
  getQueryContract,
  getUploadStatus,
  runQuery,
  runQueryDownload,
  validateLinq
} from "../api/spreadsheets";
import { connectJobProgress } from "../api/progress-hub";
import type {
  LintDiagnostic,
  OutputFormat,
  QueryContractSheet,
  QueryPreviewResponse,
  SchemaResponse,
  SpreadsheetColumn,
  SpreadsheetPreview,
  UploadStatusResponse
} from "../api/types";
import { validateNavigationPaths } from "../editor/navigation-validator";

export interface RunHistoryItem {
  at: string;
  elapsedMs?: number;
  rowCountPreview: number;
  diagnostics: number;
}

export interface SandboxState {
  file?: File;
  file2?: File;
  file3?: File;
  fileToken?: string;
  columns: SpreadsheetColumn[];
  sheets: QueryContractSheet[];
  originalPreview?: SpreadsheetPreview;
  linqCode: string;
  diagnostics: LintDiagnostic[];
  resultPreview?: QueryPreviewResponse;
  outputFormat: OutputFormat;
  schemaLoading: boolean;
  validateLoading: boolean;
  runLoading: boolean;
  downloadLoading: boolean;
  uploadStage?: string;
  uploadProgress: number;
  uploadMessage?: string;
  errorMessage?: string;
  runHistory: RunHistoryItem[];
}

const INITIAL_CODE = "return rows.Where(row => true).ToList();";
const PREVIEW_PAGE_SIZE = 250;

const initialState: SandboxState = {
  columns: [],
  sheets: [],
  linqCode: INITIAL_CODE,
  diagnostics: [],
  outputFormat: "xlsx",
  schemaLoading: false,
  validateLoading: false,
  runLoading: false,
  downloadLoading: false,
  uploadProgress: 0,
  runHistory: []
};

type Action =
  | { type: "set-file"; payload: { file: File; file2?: File; file3?: File } }
  | { type: "set-file-token"; payload: string }
  | { type: "set-schema"; payload: SchemaResponse }
  | { type: "append-sheet-preview"; payload: { sheetName: string; rows: Array<Record<string, string>>; totalRows: number } }
  | { type: "set-contract"; payload: QueryContractSheet[] }
  | { type: "set-output"; payload: OutputFormat }
  | { type: "set-code"; payload: string }
  | { type: "set-diagnostics"; payload: LintDiagnostic[] }
  | { type: "set-result"; payload: QueryPreviewResponse }
  | { type: "set-loading"; payload: Partial<Pick<SandboxState, "schemaLoading" | "validateLoading" | "runLoading" | "downloadLoading">> }
  | { type: "set-upload-status"; payload: UploadStatusResponse }
  | { type: "set-error"; payload?: string }
  | { type: "push-history"; payload: RunHistoryItem }
  | { type: "reset" };

function reducer(state: SandboxState, action: Action): SandboxState {
  if (action.type === "set-file") {
    return {
      ...state,
      file: action.payload.file,
      file2: action.payload.file2,
      file3: action.payload.file3,
      errorMessage: undefined,
      uploadProgress: 0,
      uploadStage: "Queued",
      uploadMessage: "Upload iniciado"
    };
  }

  if (action.type === "set-file-token") {
    return { ...state, fileToken: action.payload };
  }

  if (action.type === "set-schema") {
    return {
      ...state,
      fileToken: action.payload.fileToken,
      columns: action.payload.columns,
      sheets: action.payload.sheets ?? [],
      originalPreview: action.payload.preview,
      diagnostics: [],
      resultPreview: undefined,
      errorMessage: undefined
    };
  }

  if (action.type === "append-sheet-preview") {
    const target = action.payload.sheetName.toLowerCase();
    const sheets = state.sheets.length === 0
      ? [{ sheetName: action.payload.sheetName, columns: [], previewRows: [], rowCount: action.payload.totalRows }]
      : state.sheets.map((sheet) => {
        if (sheet.sheetName.toLowerCase() !== target) {
          return sheet;
        }

        const previous = sheet.previewRows ?? [];
        return {
          ...sheet,
          rowCount: action.payload.totalRows,
          previewRows: [...previous, ...action.payload.rows]
        };
      });

    const exists = sheets.some((sheet) => sheet.sheetName.toLowerCase() === target);
    const nextSheets = exists
      ? sheets
      : [...sheets, { sheetName: action.payload.sheetName, columns: [], previewRows: action.payload.rows, rowCount: action.payload.totalRows }];

    const sheet1 = nextSheets.find((s) => s.sheetName.toLowerCase() === "sheet1") ?? nextSheets[0];
    return {
      ...state,
      sheets: nextSheets,
      originalPreview: {
        rowCountPreview: sheet1?.rowCount ?? 0,
        rows: (sheet1?.previewRows ?? [])
      }
    };
  }

  if (action.type === "set-contract") {
    const columns = toColumns(action.payload);
    const primary = action.payload.find((sheet) => sheet.sheetName.toLowerCase() === "unified")
      ?? action.payload.find((sheet) => sheet.sheetName.toLowerCase() === "sheet1")
      ?? action.payload[0];

    return {
      ...state,
      sheets: action.payload,
      columns,
      originalPreview: {
        rowCountPreview: primary?.rowCount ?? 0,
        rows: primary?.previewRows ?? []
      }
    };
  }

  if (action.type === "set-output") {
    return { ...state, outputFormat: action.payload };
  }

  if (action.type === "set-code") {
    return { ...state, linqCode: action.payload };
  }

  if (action.type === "set-diagnostics") {
    return { ...state, diagnostics: action.payload };
  }

  if (action.type === "set-result") {
    return { ...state, resultPreview: action.payload };
  }

  if (action.type === "set-loading") {
    return { ...state, ...action.payload };
  }

  if (action.type === "set-upload-status") {
    return {
      ...state,
      uploadStage: action.payload.stage,
      uploadProgress: action.payload.progress,
      uploadMessage: action.payload.message
    };
  }

  if (action.type === "set-error") {
    return { ...state, errorMessage: action.payload };
  }

  if (action.type === "push-history") {
    return { ...state, runHistory: [action.payload, ...state.runHistory].slice(0, 8) };
  }

  return { ...initialState };
}

function resolveApiError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return "Unexpected error.";
}

function toColumns(sheets: QueryContractSheet[]): SpreadsheetColumn[] {
  const primary = sheets.find((x) => x.sheetName.toLowerCase() === "unified")
    ?? sheets.find((x) => x.sheetName.toLowerCase() === "sheet1")
    ?? sheets[0];
  return (primary?.columns ?? []).map((name) => ({
    originalName: name,
    normalizedName: name,
    inferredType: "string" as const
  }));
}

export function useSandboxState() {
  const [state, dispatch] = useReducer(reducer, initialState);

  const setCode = useCallback((code: string) => {
    dispatch({ type: "set-code", payload: code });
  }, []);

  const setOutputFormat = useCallback((format: OutputFormat) => {
    dispatch({ type: "set-output", payload: format });
  }, []);

  const reset = useCallback(() => {
    dispatch({ type: "reset" });
  }, []);

  const applyContract = useCallback((sheets: QueryContractSheet[]) => {
    dispatch({ type: "set-contract", payload: sheets });
  }, []);

  const uploadFile = useCallback(async (file: File, file2?: File, file3?: File) => {
    dispatch({ type: "set-file", payload: { file, file2, file3 } });
    dispatch({ type: "set-loading", payload: { schemaLoading: true } });

    const loadedPagesBySheet = new Map<string, number>();
    let stopHub: (() => Promise<void>) | undefined;

    try {
      const jobId = await createUploadJob(file, file2, file3);
      dispatch({ type: "set-file-token", payload: jobId });

      try {
        stopHub = await connectJobProgress(jobId, (payload) => {
          dispatch({ type: "set-upload-status", payload });
        });
      } catch {
        // fallback to polling only
      }

      const startedAt = Date.now();
      while (Date.now() - startedAt < 120_000) {
        const status = await getUploadStatus(jobId);
        dispatch({ type: "set-upload-status", payload: status });

        for (const sheet of status.sheetStatuses ?? []) {
          const key = sheet.sheetName.toLowerCase();
          const loadedPages = loadedPagesBySheet.get(key) ?? 0;
          const availablePages = Math.ceil(sheet.availableRows / PREVIEW_PAGE_SIZE);

          for (let page = loadedPages + 1; page <= availablePages; page++) {
            const preview = await getPreviewPage(jobId, sheet.sheetName, page, PREVIEW_PAGE_SIZE);
            const rows = preview.rows.map((row) =>
              Object.fromEntries(Object.entries(row).map(([k, v]) => [k, v ?? ""]))
            );

            dispatch({
              type: "append-sheet-preview",
              payload: {
                sheetName: sheet.sheetName,
                rows,
                totalRows: preview.totalRows
              }
            });
            loadedPagesBySheet.set(key, page);
          }
        }

        if (status.stage === "Ready") {
          const contract = await getQueryContract(jobId);
          dispatch({
            type: "set-schema",
            payload: {
              fileToken: jobId,
              columns: toColumns(contract.sheets),
              preview: {
                rows: state.originalPreview?.rows ?? [],
                rowCountPreview: state.originalPreview?.rowCountPreview ?? 0
              },
              sheets: contract.sheets.map((sheet) => {
                const existing = state.sheets.find((x) => x.sheetName.toLowerCase() === sheet.sheetName.toLowerCase());
                return {
                  ...sheet,
                  previewRows: existing?.previewRows ?? sheet.previewRows,
                  rowCount: existing?.rowCount ?? sheet.rowCount
                };
              })
            }
          });
          return;
        }

        if (status.stage === "Failed") {
          throw new Error(status.message ?? "Upload failed.");
        }

        await new Promise((resolve) => globalThis.setTimeout(resolve, 250));
      }

      throw new Error("Timeout waiting upload processing.");
    } catch (error) {
      dispatch({ type: "set-error", payload: resolveApiError(error) });
      throw error;
    } finally {
      if (stopHub) {
        await stopHub();
      }
      dispatch({ type: "set-loading", payload: { schemaLoading: false } });
    }
  }, [state.originalPreview?.rowCountPreview, state.originalPreview?.rows, state.sheets]);

  const validate = useCallback(async () => {
    if (!state.fileToken) {
      return;
    }

    dispatch({ type: "set-loading", payload: { validateLoading: true } });

    try {
      const localDiagnostics = validateNavigationPaths(state.linqCode, state.sheets);
      if (localDiagnostics.length > 0) {
        dispatch({ type: "set-diagnostics", payload: localDiagnostics });
        return;
      }

      const response = await validateLinq({ fileToken: state.fileToken, linqCode: state.linqCode });
      dispatch({ type: "set-diagnostics", payload: response.diagnostics });
    } catch (error) {
      dispatch({ type: "set-error", payload: resolveApiError(error) });
      throw error;
    } finally {
      dispatch({ type: "set-loading", payload: { validateLoading: false } });
    }
  }, [state.fileToken, state.linqCode, state.sheets]);

  const runPreview = useCallback(async () => {
    if (!state.fileToken) {
      return undefined;
    }

    dispatch({ type: "set-loading", payload: { runLoading: true } });

    try {
      const payload = {
        fileToken: state.fileToken,
        linqCode: state.linqCode,
        outputFormat: state.outputFormat
      } as const;

      const preview = await runQuery(payload);
      dispatch({ type: "set-result", payload: preview });
      dispatch({
        type: "push-history",
        payload: {
          at: new Date().toISOString(),
          elapsedMs: preview.elapsedMs,
          rowCountPreview: preview.rowCountPreview,
          diagnostics: state.diagnostics.length
        }
      });

      return preview;
    } catch (error) {
      dispatch({ type: "set-error", payload: resolveApiError(error) });
      throw error;
    } finally {
      dispatch({ type: "set-loading", payload: { runLoading: false } });
    }
  }, [state.fileToken, state.linqCode, state.outputFormat, state.diagnostics.length]);

  const downloadResult = useCallback(async () => {
    if (!state.fileToken) {
      return undefined;
    }

    dispatch({ type: "set-loading", payload: { downloadLoading: true } });

    try {
      return await runQueryDownload({
        fileToken: state.fileToken,
        linqCode: state.linqCode,
        outputFormat: state.outputFormat
      });
    } catch (error) {
      dispatch({ type: "set-error", payload: resolveApiError(error) });
      throw error;
    } finally {
      dispatch({ type: "set-loading", payload: { downloadLoading: false } });
    }
  }, [state.fileToken, state.linqCode, state.outputFormat]);

  return {
    state,
    actions: {
      setCode,
      setOutputFormat,
      uploadFile,
      validate,
      runPreview,
      downloadResult,
      applyContract,
      reset
    }
  };
}

