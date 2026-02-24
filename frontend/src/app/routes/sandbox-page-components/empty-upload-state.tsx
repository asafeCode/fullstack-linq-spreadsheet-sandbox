import { Upload } from "lucide-react";
import { tv } from "tailwind-variants";
import { Button } from "../../../shared/components/button";
import { Card, CardContent } from "../../../shared/components/card";

const emptyUploadVariants = tv({
  slots: {
    card: "h-full",
    content: "flex h-full flex-col items-center justify-center gap-4 p-5 text-center",
    iconWrap: "rounded-full border border-border bg-muted p-4",
    title: "text-lg font-semibold",
    subtitle: "text-sm text-muted-foreground"
  }
});

export interface EmptyUploadStateProps {
  onPickPrimaryFile: () => void;
}

export function EmptyUploadState({ onPickPrimaryFile }: EmptyUploadStateProps) {
  const styles = emptyUploadVariants();

  return (
    <Card data-slot="empty-upload-state" className={styles.card()}>
      <CardContent className={styles.content()}>
        <div className={styles.iconWrap()}>
          <Upload className="size-8 text-primary" />
        </div>
        <div className="max-w-xl">
          <h2 className={styles.title()}>Upload ate 3 planilhas CSV/XLSX</h2>
          <p className={styles.subtitle()}>Depois escolha a planilha mae, chave primaria e mapeie as colunas de comparacao.</p>
        </div>
        <Button onClick={onPickPrimaryFile} size="sm">
          <Upload className="size-4" />
          Escolher planilha 1
        </Button>
      </CardContent>
    </Card>
  );
}
