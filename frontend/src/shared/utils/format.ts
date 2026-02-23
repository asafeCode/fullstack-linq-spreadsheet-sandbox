import { twMerge } from "tailwind-merge";

export function cn(...tokens: Array<string | false | null | undefined>): string {
  return twMerge(tokens.filter(Boolean).join(" "));
}

export function formatFileSize(sizeInBytes: number): string {
  if (sizeInBytes < 1024) {
    return `${sizeInBytes} B`;
  }

  const kb = sizeInBytes / 1024;
  if (kb < 1024) {
    return `${kb.toFixed(1)} KB`;
  }

  return `${(kb / 1024).toFixed(1)} MB`;
}

export function formatElapsed(ms?: number): string {
  if (typeof ms !== "number") {
    return "-";
  }

  return `${ms.toFixed(0)} ms`;
}
