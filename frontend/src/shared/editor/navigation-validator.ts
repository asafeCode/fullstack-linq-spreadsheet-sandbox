import type { LintDiagnostic, QueryContractSheet } from "../api/types";

const NAV_PATTERN = /row\.(sheet1|sheet2)\.([A-Za-z_][A-Za-z0-9_]*)/g;

export function validateNavigationPaths(code: string, sheets: QueryContractSheet[]): LintDiagnostic[] {
  if (!code || sheets.length === 0) {
    return [];
  }

  const diagnostics: LintDiagnostic[] = [];
  const sheetMap = new Map(sheets.map((s) => [s.sheetName.toLowerCase(), new Set(s.columns.map((c) => c.toLowerCase()))]));

  for (const match of code.matchAll(NAV_PATTERN)) {
    const sheet = match[1]?.toLowerCase();
    const column = match[2]?.toLowerCase();
    if (!sheet || !column) {
      continue;
    }

    const columns = sheetMap.get(sheet);
    if (!columns) {
      diagnostics.push({
        message: `Navegacao invalida: ${sheet} nao foi carregada.`,
        severity: "error",
        line: 1,
        column: 1
      });
      continue;
    }

    if (!columns.has(column)) {
      diagnostics.push({
        message: `Coluna nao encontrada em ${sheet}: ${column}`,
        severity: "error",
        line: 1,
        column: 1
      });
    }
  }

  return diagnostics;
}
