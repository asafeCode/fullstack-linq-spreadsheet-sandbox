import * as Monaco from "monaco-editor";
import type { LintDiagnostic } from "../api/types";

const severityMap: Record<LintDiagnostic["severity"], Monaco.MarkerSeverity> = {
  info: Monaco.MarkerSeverity.Info,
  warning: Monaco.MarkerSeverity.Warning,
  error: Monaco.MarkerSeverity.Error
};

export function mapDiagnosticsToMarkers(diagnostics: LintDiagnostic[]): Monaco.editor.IMarkerData[] {
  return diagnostics.map((diagnostic) => ({
    message: diagnostic.message,
    severity: severityMap[diagnostic.severity],
    startLineNumber: diagnostic.line,
    startColumn: diagnostic.column,
    endLineNumber: diagnostic.line,
    endColumn: diagnostic.column + 1
  }));
}
