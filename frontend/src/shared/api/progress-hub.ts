import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import type { UploadStatusResponse } from "./types";

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

function resolveHubUrl(): string {
  // In production, always use same-origin hub endpoint.
  if (import.meta.env.PROD) {
    return "/hubs/query-progress";
  }

  const base = normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL) ?? window.location.origin;
  return `${base}/hubs/query-progress`;
}

export async function connectJobProgress(
  jobId: string,
  onProgress: (payload: UploadStatusResponse) => void
): Promise<() => Promise<void>> {
  const connection = new HubConnectionBuilder()
    .withUrl(resolveHubUrl())
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on("jobProgress", onProgress);

  await connection.start();
  await connection.invoke("JoinJob", jobId);

  return async () => {
    try {
      if (connection.state !== HubConnectionState.Disconnected) {
        await connection.stop();
      }
    } catch {
      // noop
    }
  };
}
