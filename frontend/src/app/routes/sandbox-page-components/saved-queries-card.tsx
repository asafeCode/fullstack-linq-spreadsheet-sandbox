import { X } from "lucide-react";
import { tv } from "tailwind-variants";
import type { SavedQuery } from "../../../shared/api/types";
import { Button } from "../../../shared/components/button";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/card";
import { Input } from "../../../shared/components/input";

const savedQueriesVariants = tv({
  slots: {
    content: "space-y-3",
    row: "grid gap-2 sm:grid-cols-[1fr_auto]",
    listItem: "flex items-center gap-2 rounded-xl border border-border bg-surface p-2",
    itemButton: "flex-1 text-left text-sm hover:underline"
  }
});

export interface SavedQueriesCardProps {
  savedQueries: SavedQuery[];
  savedQueryName: string;
  selectedSavedQueryId: number | null;
  loading: boolean;
  busy: boolean;
  onSavedQueryNameChange: (value: string) => void;
  onSaveCurrentQuery: () => void;
  onLoadSavedQuery: (id: number) => void;
  onDeleteSavedQuery: (id: number) => void;
}

export function SavedQueriesCard({
  savedQueries,
  savedQueryName,
  selectedSavedQueryId,
  loading,
  busy,
  onSavedQueryNameChange,
  onSaveCurrentQuery,
  onLoadSavedQuery,
  onDeleteSavedQuery
}: SavedQueriesCardProps) {
  const styles = savedQueriesVariants();

  return (
    <Card data-slot="saved-queries-card">
      <CardHeader>
        <CardTitle>Queries salvas</CardTitle>
      </CardHeader>
      <CardContent className={styles.content()}>
        <div className={styles.row()}>
          <Input
            data-slot="saved-query-name-input"
            value={savedQueryName}
            onChange={(event) => onSavedQueryNameChange(event.target.value)}
            placeholder="Ex: Mesclar pagamentos com clientes ativos"
            maxLength={200}
          />
          <Button data-slot="save-query-button" onClick={onSaveCurrentQuery} disabled={busy}>
            Salvar query
          </Button>
        </div>

        {loading ? (
          <p className="text-sm text-muted-foreground">Carregando queries...</p>
        ) : savedQueries.length === 0 ? (
          <p className="text-sm text-muted-foreground">Nenhuma query salva ainda.</p>
        ) : (
          <ul className="space-y-2">
            {savedQueries.map((query) => (
              <li key={query.id} className={styles.listItem()}>
                <button
                  type="button"
                  data-slot="saved-query-item"
                  data-saved-query-id={query.id}
                  className={styles.itemButton()}
                  onClick={() => onLoadSavedQuery(query.id)}
                >
                  <span className={selectedSavedQueryId === query.id ? "font-semibold text-primary" : "font-medium"}>
                    {query.name}
                  </span>
                </button>
                <Button
                  variant="ghost"
                  data-slot="delete-saved-query-button"
                  data-saved-query-id={query.id}
                  onClick={() => onDeleteSavedQuery(query.id)}
                  disabled={busy}
                  aria-label="Excluir query salva"
                >
                  <X className="size-4" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

