export type InferredType = "string" | "decimal" | "datetime" | "bool";

export interface SpreadsheetColumn {
  originalName: string;
  normalizedName: string;
  inferredType: InferredType;
}

export interface SpreadsheetPreview {
  rows: Array<Record<string, string>>;
  rowCountPreview: number;
}

export interface SchemaResponse {
  fileToken: string;
  columns: SpreadsheetColumn[];
  preview: SpreadsheetPreview;
  sheets?: QueryContractSheet[];
}

export type DiagnosticSeverity = "info" | "warning" | "error";

export interface LintDiagnostic {
  message: string;
  line: number;
  column: number;
  severity: DiagnosticSeverity;
}

export interface ValidateResponse {
  diagnostics: LintDiagnostic[];
}

export type OutputFormat = "csv" | "xlsx";

export interface ValidateRequest {
  fileToken: string;
  linqCode: string;
}

export interface QueryRequest {
  fileToken: string;
  linqCode: string;
  outputFormat: OutputFormat;
}

export interface QueryPreviewResponse {
  rows: Array<Record<string, string>>;
  rowCountPreview: number;
  elapsedMs?: number;
}

export interface QueryContractSheet {
  sheetName: string;
  columns: string[];
  rowCount?: number;
  previewRows?: Array<Record<string, string>>;
}

export interface UploadSheetStatus {
  sheetName: string;
  availableRows: number;
  totalRows: number;
  completed: boolean;
}

export interface UploadStatusResponse {
  jobId: string;
  stage: "Queued" | "Parsing" | "Ready" | "Executing" | "Completed" | "Failed";
  progress: number;
  message?: string;
  sheetStatuses?: UploadSheetStatus[];
}

export interface PreviewPageResponse {
  sheetName: string;
  page: number;
  pageSize: number;
  totalRows: number;
  rows: Array<Record<string, string>>;
}

export interface QueryContractResponse {
  variables: string[];
  allowedMethods: string[];
  snippets: string[];
  sheets: QueryContractSheet[];
}

export interface UnifyComparisonRequest {
  sheetName: string;
  compareColumn: string;
}

export interface UnifySheetsRequest {
  primarySheetName: string;
  primaryKeyColumn: string;
  comparisons: UnifyComparisonRequest[];
}

export interface ApiProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
