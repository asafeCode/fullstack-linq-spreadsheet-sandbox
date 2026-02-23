import { Download } from "lucide-react";
import type { QueryPreviewResponse } from "../api/types";
import { Badge } from "../components/badge";
import { Button } from "../components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../components/card";
import { formatElapsed } from "../utils/format";
import { DataGrid } from "./data-grid";

export interface ResultViewerProps {
  preview?: QueryPreviewResponse;
  onDownload: () => void;
  canDownload: boolean;
}

export function ResultViewer({ preview, onDownload, canDownload }: ResultViewerProps) {
  return (
    <Card data-slot="result-viewer" className="h-full">
      <CardHeader>
        <div>
          <CardTitle>Query result</CardTitle>
          <p className="mt-1 text-xs text-muted-foreground">Elapsed: {formatElapsed(preview?.elapsedMs)}</p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="success">{preview?.rowCountPreview ?? 0} rows</Badge>
          <Button variant="secondary" size="sm" aria-label="Download result" onClick={onDownload} disabled={!canDownload}>
            <Download className="h-4 w-4" />
            Download
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        <DataGrid rows={preview?.rows ?? []} emptyMessage="Run a LINQ query to see result preview." />
      </CardContent>
    </Card>
  );
}

