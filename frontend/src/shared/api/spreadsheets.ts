import { apiClient } from "./client";
import type {
  OutputFormat,
  PreviewPageResponse,
  QueryContractResponse,
  QueryPreviewResponse,
  SchemaResponse,
  UnifySheetsRequest,
  UploadStatusResponse,
  ValidateResponse
} from "./types";

const QUERY_UPLOAD_PATH = "/api/query/upload";
const QUERY_STATUS_PATH = "/api/query/status";
const QUERY_EXECUTE_PATH = "/api/query";
const QUERY_EXECUTE_STATUS_PATH = "/api/query/execute";
const QUERY_CONTRACT_PATH = "/api/query/contract";
const QUERY_PREVIEW_PATH = "/api/query";

const POLL_INTERVAL_MS = 500;
const POLL_TIMEOUT_MS = 60_000;

interface QueryUploadResponse {
  jobId: string;
}

interface QueryExecuteResponse {
  queryId: string;
}

interface QueryExecuteStatusResponse {
  queryId: string;
  stage: "Queued" | "Parsing" | "Ready" | "Executing" | "Completed" | "Failed";
  success: boolean;
  rows: Array<Record<string, unknown>>;
  diagnostics: ValidateResponse["diagnostics"];
  elapsedMs?: number;
}

interface QueryRequest {
  fileToken: string;
  linqCode: string;
  outputFormat: OutputFormat;
}

export async function uploadSpreadsheet(file: File, file2?: File): Promise<SchemaResponse> {
  const jobId = await createUploadJob(file, file2);
  const status = await pollJobReady(jobId);
  if (status.stage !== "Ready") {
    throw new Error(status.message ?? "Upload processing failed.");
  }
  const contract = await getQueryContract(jobId);
  const primarySheet = contract.sheets.find((x) => x.sheetName.toLowerCase() === "sheet1") ?? contract.sheets[0];
  const columns = (primarySheet?.columns ?? []).map((name) => ({
    originalName: name,
    normalizedName: name,
    inferredType: "string" as const
  }));
  const previewRows = (primarySheet?.previewRows ?? []).map((row) =>
    Object.fromEntries(Object.entries(row).map(([key, value]) => [key, value ?? ""]))
  );

  return {
    fileToken: jobId,
    columns,
    preview: {
      rows: previewRows,
      rowCountPreview: primarySheet?.rowCount ?? previewRows.length
    },
    sheets: contract.sheets
  };
}

export async function createUploadJob(file: File, file2?: File, file3?: File): Promise<string> {
  const formData = new FormData();
  formData.append("file1", file);
  if (file2) {
    formData.append("file2", file2);
  }
  if (file3) {
    formData.append("file3", file3);
  }

  const upload = await apiClient.post<QueryUploadResponse>(QUERY_UPLOAD_PATH, formData, {
    headers: {
      "Content-Type": "multipart/form-data"
    }
  });

  return upload.data.jobId;
}

export async function unifySheets(jobId: string, payload: UnifySheetsRequest): Promise<void> {
  await apiClient.post(`${QUERY_PREVIEW_PATH}/${jobId}/unify`, payload);
}

export async function getUploadStatus(jobId: string): Promise<UploadStatusResponse> {
  const response = await apiClient.get<UploadStatusResponse>(`${QUERY_STATUS_PATH}/${jobId}`);
  return response.data;
}

export async function getQueryContract(fileToken?: string): Promise<QueryContractResponse> {
  const response = await apiClient.get<QueryContractResponse>(QUERY_CONTRACT_PATH, {
    params: fileToken ? { jobId: fileToken } : undefined
  });
  return response.data;
}

export async function getPreviewPage(jobId: string, sheetName: string, page: number, pageSize = 200): Promise<PreviewPageResponse> {
  const response = await apiClient.get<PreviewPageResponse>(`${QUERY_PREVIEW_PATH}/${jobId}/preview/${sheetName}`, {
    params: { page, pageSize }
  });
  return response.data;
}

export async function validateLinq(payload: { fileToken: string; linqCode: string }): Promise<ValidateResponse> {
  const response = await apiClient.post<ValidateResponse>("/api/spreadsheets/validate", payload);
  return response.data;
}

export async function runQueryPreview(payload: QueryRequest): Promise<QueryPreviewResponse> {
  const execute = await apiClient.post<QueryExecuteResponse>(`${QUERY_EXECUTE_PATH}/${payload.fileToken}/execute`, {
    code: payload.linqCode,
    maxRows: 2000,
    timeoutMs: 4000
  });
  const status = await pollQueryDone(execute.data.queryId);

  const rows = (status.rows ?? []).map((row) =>
    Object.fromEntries(Object.entries(row).map(([key, value]) => [key, value == null ? "" : String(value)]))
  );

  return {
    rows,
    rowCountPreview: rows.length,
    elapsedMs: status.elapsedMs
  };
}

function inferFileName(contentDisposition: string | null, format: OutputFormat): string {
  if (contentDisposition) {
    const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(contentDisposition);
    const encoded = match?.[1] ?? match?.[2];
    if (encoded) {
      return decodeURIComponent(encoded);
    }
  }

  return `linq-result.${format}`;
}

export async function runQueryDownload(payload: QueryRequest): Promise<{ blob: Blob; fileName: string }> {
  const preview = await runQueryPreview(payload);
  const headers = Array.from(new Set(preview.rows.flatMap((x) => Object.keys(x))));
  const lines = [headers.join(",")];
  for (const row of preview.rows) {
    lines.push(headers.map((h) => JSON.stringify(row[h] ?? "")).join(","));
  }

  const blob = new Blob([lines.join("\n")], { type: "text/csv" });
  const fileName = inferFileName(null, payload.outputFormat);
  return { blob, fileName };
}

export async function runQuery(payload: QueryRequest): Promise<QueryPreviewResponse> {
  try {
    return await runQueryPreview(payload);
  } catch {
    return {
      rows: [],
      rowCountPreview: 0
    };
  }
}

async function pollJobReady(jobId: string): Promise<UploadStatusResponse> {
  const start = Date.now();
  while (Date.now() - start < POLL_TIMEOUT_MS) {
    const status = await getUploadStatus(jobId);
    if (status.stage === "Ready" || status.stage === "Failed") {
      return status;
    }

    await delay(POLL_INTERVAL_MS);
  }

  throw new Error("Timeout waiting upload job.");
}

async function pollQueryDone(queryId: string): Promise<QueryExecuteStatusResponse> {
  const start = Date.now();
  while (Date.now() - start < POLL_TIMEOUT_MS) {
    const response = await apiClient.get<QueryExecuteStatusResponse>(`${QUERY_EXECUTE_STATUS_PATH}/${queryId}`);
    if (response.data.stage === "Completed" || response.data.stage === "Failed") {
      return response.data;
    }

    await delay(POLL_INTERVAL_MS);
  }

  throw new Error("Timeout waiting query execution.");
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    globalThis.setTimeout(resolve, ms);
  });
}

