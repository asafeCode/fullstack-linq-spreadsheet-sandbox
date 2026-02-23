import { createContext, useCallback, useContext, useMemo, useState, type HTMLAttributes, type ReactNode } from "react";
import { cn } from "../utils/format";

interface DialogContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const DialogContext = createContext<DialogContextValue | null>(null);

function useDialogContext(): DialogContextValue {
  const context = useContext(DialogContext);
  if (!context) {
    throw new Error("Dialog components must be used inside DialogRoot.");
  }

  return context;
}

interface DialogRootProps {
  children?: ReactNode;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}

export function DialogRoot({ children, open: openProp, onOpenChange }: DialogRootProps) {
  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = openProp !== undefined;
  const open = isControlled ? openProp : internalOpen;

  const setOpen = useCallback((nextOpen: boolean) => {
    if (!isControlled) {
      setInternalOpen(nextOpen);
    }

    onOpenChange?.(nextOpen);
  }, [isControlled, onOpenChange]);

  const context = useMemo<DialogContextValue>(() => ({ open, setOpen }), [open, setOpen]);

  return <DialogContext.Provider value={context}>{children}</DialogContext.Provider>;
}

export function DialogTrigger({ children }: { children?: ReactNode }) {
  const { setOpen } = useDialogContext();
  return (
    <div data-slot="dialog-trigger" onClick={() => setOpen(true)}>
      {children}
    </div>
  );
}

export function DialogPortal({ children }: { children?: ReactNode }) {
  return <>{children}</>;
}

export function DialogBackdrop({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  const { open, setOpen } = useDialogContext();
  if (!open) {
    return null;
  }

  return (
    <div
      data-slot="dialog-backdrop"
      className={cn("fixed inset-0 z-40 bg-black/30", className)}
      onClick={() => setOpen(false)}
      {...props}
    />
  );
}

export function DialogContent({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  const { open } = useDialogContext();
  if (!open) {
    return null;
  }

  return (
    <div
      data-slot="dialog-content"
      className={cn(
        "fixed left-1/2 top-1/2 z-50 w-[min(92vw,36rem)] -translate-x-1/2 -translate-y-1/2 rounded-2xl border border-border bg-surface-raised p-5 shadow-xl",
        className
      )}
      {...props}
    >
      {children}
    </div>
  );
}

export function DialogTitle({ className, ...props }: HTMLAttributes<HTMLHeadingElement>) {
  return <h2 data-slot="dialog-title" className={cn("text-base font-semibold", className)} {...props} />;
}

export function DialogDescription({ className, ...props }: HTMLAttributes<HTMLParagraphElement>) {
  return <p data-slot="dialog-description" className={cn("mt-2 text-sm text-muted-foreground", className)} {...props} />;
}

export function DialogClose({ children }: { children?: ReactNode }) {
  const { setOpen } = useDialogContext();
  return (
    <div data-slot="dialog-close" onClick={() => setOpen(false)}>
      {children}
    </div>
  );
}
