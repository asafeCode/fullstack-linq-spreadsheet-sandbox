import { Plus, X } from "lucide-react";
import { tv } from "tailwind-variants";
import type { SpreadsheetColumn } from "../../../shared/api/types";
import { Button } from "../../../shared/components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/card";
import { Input } from "../../../shared/components/input";
import {
  SelectItem,
  SelectPopup,
  SelectPortal,
  SelectPositioner,
  SelectRoot,
  SelectTrigger,
  SelectValue
} from "../../../shared/components/select";
import type { FilterOperator, ProjectionField, SimpleFilter } from "./sandbox-builder.types";

const builderCardVariants = tv({
  slots: {
    content: "space-y-3",
    block: "space-y-2 rounded-xl border border-border p-3",
    projectionBlock: "space-y-2 rounded-xl border border-border bg-surface p-3",
    generatedCodeBlock: "space-y-2 rounded-xl border border-border bg-surface p-3",
    generatedCodeText: "max-h-56 overflow-auto rounded-lg border border-border bg-surface-raised p-3 font-mono text-xs text-foreground",
    twoCols: "grid gap-2 sm:grid-cols-2",
    filterRow: "grid gap-2 sm:grid-cols-[1fr_auto_auto_auto]",
    projectionRow: "grid gap-2 sm:grid-cols-[1fr_1fr_auto]"
  }
});

export interface FilterBuilderCardProps {
  columns: SpreadsheetColumn[];
  filters: SimpleFilter[];
  projections: ProjectionField[];
  generatedCode?: string;
  runLoading?: boolean;
  onFilterChange: (id: string, patch: Partial<SimpleFilter>) => void;
  onRemoveFilter: (id: string) => void;
  onAddFilter: () => void;
  onProjectionChange: (id: string, patch: Partial<ProjectionField>) => void;
  onRemoveProjection: (id: string) => void;
  onAddProjection: () => void;
  onApplyGeneratedCode: () => void;
  onOpenCodeEditor: () => void;
  onRunQuery: () => void;
}

export function FilterBuilderCard({
  columns,
  filters,
  projections,
  generatedCode,
  runLoading,
  onFilterChange,
  onRemoveFilter,
  onAddFilter,
  onProjectionChange,
  onRemoveProjection,
  onAddProjection,
  onApplyGeneratedCode,
  onOpenCodeEditor,
  onRunQuery
}: FilterBuilderCardProps) {
  const styles = builderCardVariants();

  return (
    <Card data-slot="filter-builder-card">
      <CardHeader>
        <CardTitle>Filtros simples e projection</CardTitle>
      </CardHeader>
      <CardContent className={styles.content()}>
        {filters.map((filter, index) => (
          <div key={filter.id} className={styles.block()}>
            <div className={styles.twoCols()}>
              <SelectRoot value={filter.column} onValueChange={(value) => onFilterChange(filter.id, { column: value })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectPortal><SelectPositioner><SelectPopup>
                  {columns.map((column) => (
                    <SelectItem key={column.normalizedName} value={column.normalizedName}>{column.normalizedName}</SelectItem>
                  ))}
                </SelectPopup></SelectPositioner></SelectPortal>
              </SelectRoot>

              <SelectRoot value={filter.operator} onValueChange={(value) => onFilterChange(filter.id, { operator: value as FilterOperator })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectPortal><SelectPositioner><SelectPopup>
                  <SelectItem value="equals">Igual (normalizado)</SelectItem>
                  <SelectItem value="contains">Contem</SelectItem>
                  <SelectItem value="startsWith">Comeca com</SelectItem>
                  <SelectItem value="endsWith">Termina com</SelectItem>
                  <SelectItem value="isEmpty">Vazio</SelectItem>
                </SelectPopup></SelectPositioner></SelectPortal>
              </SelectRoot>
            </div>

            <div className={styles.filterRow()}>
              <Input
                value={filter.value}
                onChange={(event) => onFilterChange(filter.id, { value: event.target.value })}
                placeholder="Valor esperado"
                disabled={filter.operator === "isEmpty"}
              />
              <Button variant={filter.negate ? "primary" : "secondary"} onClick={() => onFilterChange(filter.id, { negate: !filter.negate })}>NOT</Button>
              {index > 0 ? (
                <SelectRoot value={filter.joinWith} onValueChange={(value) => onFilterChange(filter.id, { joinWith: value as "AND" | "OR" })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectPortal><SelectPositioner><SelectPopup>
                    <SelectItem value="AND">AND</SelectItem>
                    <SelectItem value="OR">OR</SelectItem>
                  </SelectPopup></SelectPositioner></SelectPortal>
                </SelectRoot>
              ) : <div />}
              <Button variant="ghost" onClick={() => onRemoveFilter(filter.id)} aria-label="Remover filtro">
                <X className="size-4" />
              </Button>
            </div>
          </div>
        ))}

        <Button variant="secondary" onClick={onAddFilter}><Plus className="size-4" />Adicionar filtro</Button>

        <div className={styles.projectionBlock()}>
          <h3 className="text-sm font-semibold">Projection</h3>
          {projections.map((projection) => (
            <div key={projection.id} className={styles.projectionRow()}>
              <Input
                value={projection.alias}
                onChange={(event) => onProjectionChange(projection.id, { alias: event.target.value })}
                placeholder="Novo nome (opcional)"
              />
              <SelectRoot value={projection.column} onValueChange={(value) => onProjectionChange(projection.id, { column: value })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectPortal><SelectPositioner><SelectPopup>
                  {columns.map((column) => (
                    <SelectItem key={`proj-${column.normalizedName}`} value={column.normalizedName}>{column.normalizedName}</SelectItem>
                  ))}
                </SelectPopup></SelectPositioner></SelectPortal>
              </SelectRoot>
              <Button variant="ghost" onClick={() => onRemoveProjection(projection.id)} aria-label="Remover coluna">
                <X className="size-4" />
              </Button>
            </div>
          ))}

          <Button variant="secondary" onClick={onAddProjection}><Plus className="size-4" />Adicionar coluna</Button>
        </div>

        <div className="grid gap-2 sm:grid-cols-2">
          <Button onClick={onApplyGeneratedCode}>Gerar LINQ pelos filtros</Button>
          <Button variant="secondary" onClick={onOpenCodeEditor}>Escrever filtros</Button>
        </div>

        {generatedCode ? (
          <div data-slot="generated-linq-card" className={styles.generatedCodeBlock()}>
            <h3 className="text-sm font-semibold">LINQ gerada</h3>
            <pre className={styles.generatedCodeText()}>{generatedCode}</pre>
            <Button onClick={onRunQuery} disabled={Boolean(runLoading)}>
              {runLoading ? "Executando..." : "Run query"}
            </Button>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
