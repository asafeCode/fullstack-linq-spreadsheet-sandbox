import { useMemo, useState } from "react";
import type { QueryContractSheet, SpreadsheetPreview } from "../api/types";
import { Badge } from "../components/badge";
import { Button } from "../components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../components/card";
import { DataGrid } from "./data-grid";

export interface SpreadsheetViewerProps {
  preview?: SpreadsheetPreview;
  sheets?: QueryContractSheet[];
}

export function SpreadsheetViewer({ preview, sheets }: SpreadsheetViewerProps) {
  const [activeSheet, setActiveSheet] = useState<string>("sheet1");
  const availableSheets = sheets ?? [];

  const currentRows = useMemo(() => {
    if (availableSheets.length === 0) {
      return preview?.rows ?? [];
    }

    const selected =
      availableSheets.find((sheet) => sheet.sheetName.toLowerCase() === activeSheet.toLowerCase()) ?? availableSheets[0];
    if (!selected) {
      return [];
    }

    return selected.previewRows ?? [];
  }, [activeSheet, availableSheets, preview?.rows]);

  const rowCount = useMemo(() => {
    if (availableSheets.length === 0) {
      return preview?.rowCountPreview ?? 0;
    }

    const selected =
      availableSheets.find((sheet) => sheet.sheetName.toLowerCase() === activeSheet.toLowerCase()) ?? availableSheets[0];
    if (!selected) {
      return 0;
    }

    return selected.rowCount ?? selected.previewRows?.length ?? 0;
  }, [activeSheet, availableSheets, preview?.rowCountPreview]);

  return (
    <Card data-slot="spreadsheet-viewer" className="h-full">
      <CardHeader>
        <CardTitle>Original spreadsheet preview</CardTitle>
        <Badge variant="info">{rowCount} rows</Badge>
      </CardHeader>
      <CardContent>
        {availableSheets.length > 0 ? (
          <div className="mb-3 flex items-center gap-2">
            {availableSheets.map((sheet) => {
              const isActive = sheet.sheetName.toLowerCase() === activeSheet.toLowerCase();
              return (
                <Button
                  key={sheet.sheetName}
                  variant={isActive ? "primary" : "secondary"}
                  size="sm"
                  onClick={() => setActiveSheet(sheet.sheetName)}
                >
                  {sheet.sheetName}
                </Button>
              );
            })}
          </div>
        ) : null}
        <DataGrid rows={currentRows} emptyMessage="Upload a spreadsheet to start." />
      </CardContent>
    </Card>
  );
}

