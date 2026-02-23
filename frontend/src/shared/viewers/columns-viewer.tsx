import { Copy } from "lucide-react";
import type { SpreadsheetColumn } from "../api/types";
import { Badge } from "../components/badge";
import { Button } from "../components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../components/card";

export interface ColumnsViewerProps {
  columns: SpreadsheetColumn[];
}

export function ColumnsViewer({ columns }: ColumnsViewerProps) {
  return (
    <Card data-slot="columns-viewer" className="h-full">
      <CardHeader>
        <CardTitle>Column mapping</CardTitle>
        <Badge variant="info">{columns.length} columns</Badge>
      </CardHeader>
      <CardContent>
        <div className="overflow-auto rounded-xl border border-border">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-muted/40 text-foreground-subtle">
              <tr>
                <th className="border-b border-border px-3 py-2">Original</th>
                <th className="border-b border-border px-3 py-2">Normalized</th>
                <th className="border-b border-border px-3 py-2">Type</th>
                <th className="border-b border-border px-3 py-2">Action</th>
              </tr>
            </thead>
            <tbody>
              {columns.map((column) => (
                <tr key={column.normalizedName} className="border-b border-border">
                  <td className="px-3 py-2">{column.originalName}</td>
                  <td className="px-3 py-2 font-mono">{column.normalizedName}</td>
                  <td className="px-3 py-2">{column.inferredType}</td>
                  <td className="px-3 py-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      aria-label={`Copy ${column.normalizedName}`}
                      onClick={() => void navigator.clipboard.writeText(`row.${column.normalizedName}`)}
                    >
                      <Copy className="h-4 w-4" />
                      Copy
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}



