import { History } from "lucide-react";
import { tv } from "tailwind-variants";
import { twMerge } from "tailwind-merge";
import { Badge } from "../../../shared/components/badge";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/card";
import { formatElapsed } from "../../../shared/utils/format";

const historyCardVariants = tv({
  slots: {
    listItem: "flex items-center justify-between rounded-xl border border-border bg-surface p-2 text-sm",
    leftText: "inline-flex items-center gap-2 text-foreground-subtle",
    rightText: "text-xs text-muted-foreground"
  }
});

export interface ExecutionHistoryItem {
  at: string;
  elapsedMs?: number;
  rowCountPreview: number;
  diagnostics: number;
}

export interface ExecutionHistoryCardProps {
  runHistory: ExecutionHistoryItem[];
  className?: string;
}

export function ExecutionHistoryCard({ runHistory, className }: ExecutionHistoryCardProps) {
  const styles = historyCardVariants();

  return (
    <Card data-slot="execution-history-card" className={twMerge("h-full", className)}>
      <CardHeader>
        <CardTitle>Execution history</CardTitle>
        <Badge variant="info">{runHistory.length}</Badge>
      </CardHeader>
      <CardContent className="overflow-auto">
        {runHistory.length === 0 ? (
          <p className="text-sm text-muted-foreground">No runs yet.</p>
        ) : (
          <ul className="space-y-2">
            {runHistory.map((entry) => (
              <li key={entry.at} className={styles.listItem()}>
                <span className={styles.leftText()}>
                  <History className="size-4" />
                  {new Date(entry.at).toLocaleTimeString()} · {entry.rowCountPreview} rows
                </span>
                <span className={styles.rightText()}>
                  {formatElapsed(entry.elapsedMs)} · {entry.diagnostics} diagnostics
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
