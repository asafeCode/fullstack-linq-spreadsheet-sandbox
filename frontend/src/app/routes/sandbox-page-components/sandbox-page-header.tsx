import { FileSpreadsheet, History, RotateCcw, Upload } from "lucide-react";
import { tv } from "tailwind-variants";
import type { RefObject } from "react";
import { Button } from "../../../shared/components/button";

export const sandboxHeaderVariants = tv({
  slots: {
    header: "z-20 border-b border-border bg-surface/95 backdrop-blur",
    inner: "mx-auto flex min-h-16 w-full max-w-[1400px] flex-wrap items-start gap-2 px-3 py-2 sm:items-center sm:gap-3 sm:px-4",
    titleWrap: "flex min-w-0 items-center gap-2",
    logoBox: "rounded-xl border border-border bg-surface-raised p-2",
    title: "text-sm font-semibold sm:text-base",
    subtitle: "max-w-[48vw] truncate text-xs text-muted-foreground sm:max-w-none",
    controls: "flex w-full flex-wrap items-center gap-2 sm:ml-auto sm:w-auto sm:justify-end"
  }
});

export interface SandboxPageHeaderProps {
  fileStatus: string;
  inputRef1: RefObject<HTMLInputElement | null>;
  inputRef2: RefObject<HTMLInputElement | null>;
  inputRef3: RefObject<HTMLInputElement | null>;
  onSelectFile1: (file?: File) => void;
  onSelectFile2: (file?: File) => void;
  onSelectFile3: (file?: File) => void;
  onReset: () => void;
  runHistoryCount: number;
  onOpenHistory: () => void;
}

export function SandboxPageHeader({
  fileStatus,
  inputRef1,
  inputRef2,
  inputRef3,
  onSelectFile1,
  onSelectFile2,
  onSelectFile3,
  onReset,
  runHistoryCount,
  onOpenHistory
}: SandboxPageHeaderProps) {
  const styles = sandboxHeaderVariants();

  return (
    <header data-slot="sandbox-page-header" className={styles.header()}>
      <div className={styles.inner()}>
        <div className={styles.titleWrap()}>
          <div className={styles.logoBox()}>
            <FileSpreadsheet className="size-5 text-primary" />
          </div>
          <div className="min-w-0">
            <h1 className={styles.title()}>LINQ Spreadsheet Sandbox</h1>
            <p className={styles.subtitle()}>{fileStatus}</p>
          </div>
        </div>

        <div className={styles.controls()}>
          <input
            ref={inputRef1}
            type="file"
            className="hidden"
            accept=".csv,.xlsx"
            onChange={(event) => onSelectFile1(event.target.files?.[0])}
          />
          <input
            ref={inputRef2}
            type="file"
            className="hidden"
            accept=".csv,.xlsx"
            onChange={(event) => onSelectFile2(event.target.files?.[0])}
          />
          <input
            ref={inputRef3}
            type="file"
            className="hidden"
            accept=".csv,.xlsx"
            onChange={(event) => onSelectFile3(event.target.files?.[0])}
          />

          <Button variant="secondary" size="sm" onClick={() => inputRef1.current?.click()} aria-label="Upload file 1">
            <Upload className="size-4" />
            <span className="hidden sm:inline">Upload 1</span>
            <span className="sm:hidden">1</span>
          </Button>
          <Button variant="secondary" size="sm" onClick={() => inputRef2.current?.click()} aria-label="Upload file 2">
            <Upload className="size-4" />
            <span className="hidden sm:inline">Upload 2</span>
            <span className="sm:hidden">2</span>
          </Button>
          <Button variant="secondary" size="sm" onClick={() => inputRef3.current?.click()} aria-label="Upload file 3">
            <Upload className="size-4" />
            <span className="hidden sm:inline">Upload 3</span>
            <span className="sm:hidden">3</span>
          </Button>
          <Button variant="ghost" size="sm" onClick={onReset} aria-label="Reset sandbox">
            <RotateCcw className="size-4" />
            <span className="hidden sm:inline">Reset</span>
          </Button>
          <Button variant="ghost" size="sm" onClick={onOpenHistory} aria-label="Abrir historico">
            <History className="size-4" />
            {runHistoryCount}
          </Button>
        </div>
      </div>
    </header>
  );
}
