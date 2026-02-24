import { Merge } from "lucide-react";
import { tv } from "tailwind-variants";
import type { QueryContractSheet } from "../../../shared/api/types";
import { Button } from "../../../shared/components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/card";
import {
  SelectItem,
  SelectPopup,
  SelectPortal,
  SelectPositioner,
  SelectRoot,
  SelectTrigger,
  SelectValue
} from "../../../shared/components/select";

const unifyCardVariants = tv({
  slots: {
    content: "space-y-3",
    grid: "grid gap-2 sm:grid-cols-2",
    sheetLabel: "rounded-xl border border-border bg-muted px-3 py-2 text-sm"
  }
});

export interface UnifySheetsCardProps {
  sheets: QueryContractSheet[];
  primaryColumns: string[];
  childSheets: QueryContractSheet[];
  primarySheet: string;
  primaryKeyColumn: string;
  comparisonColumns: Record<string, string>;
  unifyLoading: boolean;
  hasFileToken: boolean;
  onPrimarySheetChange: (value: string) => void;
  onPrimaryKeyColumnChange: (value: string) => void;
  onComparisonColumnChange: (sheetName: string, value: string) => void;
  onUnify: () => void;
}

export function UnifySheetsCard({
  sheets,
  primaryColumns,
  childSheets,
  primarySheet,
  primaryKeyColumn,
  comparisonColumns,
  unifyLoading,
  hasFileToken,
  onPrimarySheetChange,
  onPrimaryKeyColumnChange,
  onComparisonColumnChange,
  onUnify
}: UnifySheetsCardProps) {
  const styles = unifyCardVariants();

  return (
    <Card data-slot="unify-sheets-card">
      <CardHeader>
        <CardTitle>Unificar planilhas</CardTitle>
      </CardHeader>
      <CardContent className={styles.content()}>
        <div className={styles.grid()}>
          <SelectRoot value={primarySheet} onValueChange={onPrimarySheetChange}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectPortal><SelectPositioner><SelectPopup>
              {sheets.filter((x) => x.sheetName !== "unified").map((sheet) => (
                <SelectItem key={sheet.sheetName} value={sheet.sheetName}>{sheet.sheetName}</SelectItem>
              ))}
            </SelectPopup></SelectPositioner></SelectPortal>
          </SelectRoot>

          <SelectRoot value={primaryKeyColumn} onValueChange={onPrimaryKeyColumnChange}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectPortal><SelectPositioner><SelectPopup>
              {primaryColumns.map((col) => (
                <SelectItem key={col} value={col}>{col}</SelectItem>
              ))}
            </SelectPopup></SelectPositioner></SelectPortal>
          </SelectRoot>
        </div>

        {childSheets.map((sheet) => (
          <div key={sheet.sheetName} className={styles.grid()}>
            <div className={styles.sheetLabel()}>{sheet.sheetName}</div>
            <SelectRoot
              value={comparisonColumns[sheet.sheetName] ?? ""}
              onValueChange={(value) => onComparisonColumnChange(sheet.sheetName, value)}
            >
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectPortal><SelectPositioner><SelectPopup>
                {sheet.columns.map((col) => (
                  <SelectItem key={`${sheet.sheetName}-${col}`} value={col}>{col}</SelectItem>
                ))}
              </SelectPopup></SelectPositioner></SelectPortal>
            </SelectRoot>
          </div>
        ))}

        <Button onClick={onUnify} disabled={unifyLoading || !hasFileToken || childSheets.length === 0}>
          <Merge className="size-4" />
          {unifyLoading ? "Unificando..." : "Unificar por chave primaria"}
        </Button>
      </CardContent>
    </Card>
  );
}

