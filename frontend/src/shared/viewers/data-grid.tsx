import { useMemo, useState } from "react";
import { Search, Copy } from "lucide-react";
import { Input } from "../components/input";
import { Button } from "../components/button";
import { cn } from "../utils/format";

export interface DataGridProps {
  rows: Array<Record<string, string>>;
  emptyMessage?: string;
}

const PAGE_SIZE = 12;

function copyText(text: string): void {
  void navigator.clipboard.writeText(text);
}

export function DataGrid({ rows, emptyMessage = "No rows" }: DataGridProps) {
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);

  const columns = useMemo(() => {
    const first = rows[0];
    return first ? Object.keys(first) : [];
  }, [rows]);

  const filteredRows = useMemo(() => {
    const term = query.trim().toLowerCase();
    if (!term) {
      return rows;
    }

    return rows.filter((row) =>
      Object.values(row).some((value) => (value ?? "").toLowerCase().includes(term))
    );
  }, [rows, query]);

  const pageCount = Math.max(1, Math.ceil(filteredRows.length / PAGE_SIZE));
  const currentPage = Math.min(page, pageCount);
  const pageRows = filteredRows.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <div data-slot="data-grid" className="space-y-3">
      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setPage(1);
            }}
            placeholder="Filter preview"
            className="pl-9"
          />
        </div>
      </div>

      <div className="overflow-auto rounded-xl border border-border">
        <table className="min-w-full border-separate border-spacing-0 text-left text-sm">
          <thead className="sticky top-0 z-10 bg-surface-raised">
            <tr>
              {columns.map((column) => (
                <th key={column} className="whitespace-nowrap border-b border-border px-3 py-2 font-semibold text-foreground-subtle">
                  <div className="flex min-w-36 items-center justify-between gap-2">
                    <span className="truncate" title={column}>{column}</span>
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {pageRows.length === 0 ? (
              <tr>
                <td colSpan={Math.max(columns.length, 1)} className="px-3 py-8 text-center text-muted-foreground">
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              pageRows.map((row, index) => (
                <tr key={`${index}-${Object.values(row).join("|")}`} className={cn(index % 2 === 0 ? "bg-surface" : "bg-muted/30")}>
                  {columns.map((column) => {
                    const value = row[column] ?? "";
                    const isEmpty = value.trim() === "";
                    return (
                      <td key={`${column}-${index}`} className={cn("group border-b border-border px-3 py-2 align-top", isEmpty && "text-muted-foreground")}> 
                        <div className="flex items-start justify-between gap-2">
                          <span className="max-w-52 truncate" title={value}>{isEmpty ? "NULL" : value}</span>
                          <button
                            data-slot="copy-cell"
                            type="button"
                            aria-label="Copy cell"
                            className="invisible rounded p-1 text-muted-foreground hover:bg-muted group-hover:visible"
                            onClick={() => copyText(value)}
                          >
                            <Copy className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </td>
                    );
                  })}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>
          {filteredRows.length} rows · page {currentPage}/{pageCount}
        </span>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={currentPage <= 1}>
            Prev
          </Button>
          <Button variant="secondary" size="sm" onClick={() => setPage((p) => Math.min(pageCount, p + 1))} disabled={currentPage >= pageCount}>
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}

