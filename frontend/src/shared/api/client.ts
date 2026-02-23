import axios, { AxiosError } from "axios";
import type { AxiosInstance } from "axios";
import type { ApiProblemDetails } from "./types";

export class ApiError extends Error {
  public readonly status: number;

  public readonly details?: ApiProblemDetails;

  public constructor(message: string, status: number, details?: ApiProblemDetails) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.details = details;
  }
}

const DEFAULT_TIMEOUT_MS = 15000;

function normalizeBaseUrl(value: unknown): string | undefined {
  if (typeof value !== "string") {
    return undefined;
  }

  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return undefined;
  }

  return trimmed.replace(/\/$/, "");
}

function resolveBaseUrl(): string {
  // In production, always call same-origin (/api via frontend proxy).
  if (import.meta.env.PROD) {
    return "";
  }

  return normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL) ?? "";
}

function resolveMessage(status: number, details?: ApiProblemDetails): string {
  if (details?.detail) {
    return details.detail;
  }

  if (details?.title) {
    return details.title;
  }

  return `Request failed with status ${status}`;
}

function mapAxiosError(error: unknown): ApiError {
  if (!(error instanceof AxiosError)) {
    return new ApiError("Unexpected request error.", 500);
  }

  if (error.code === "ECONNABORTED") {
    return new ApiError("Request timed out.", 408);
  }

  const status = error.response?.status ?? 500;
  const details = error.response?.data as ApiProblemDetails | undefined;
  return new ApiError(resolveMessage(status, details), status, details);
}

export const apiClient: AxiosInstance = axios.create({
  baseURL: resolveBaseUrl(),
  timeout: DEFAULT_TIMEOUT_MS,
  headers: {
    Accept: "application/json"
  }
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => Promise.reject(mapAxiosError(error))
);
