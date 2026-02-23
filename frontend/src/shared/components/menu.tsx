import { createContext, useContext, useMemo, useState, type HTMLAttributes, type ReactNode } from "react";
import { cn } from "../utils/format";

interface MenuContextValue {
  open: boolean;
  setOpen: (open: boolean) => void;
}

const MenuContext = createContext<MenuContextValue | null>(null);

function useMenuContext(): MenuContextValue {
  const context = useContext(MenuContext);
  if (!context) {
    throw new Error("Menu components must be used inside MenuRoot.");
  }

  return context;
}

export function MenuRoot({ children }: { children?: ReactNode }) {
  const [open, setOpen] = useState(false);
  const context = useMemo<MenuContextValue>(() => ({ open, setOpen }), [open]);

  return (
    <MenuContext.Provider value={context}>
      <div data-slot="menu-root" className="relative inline-block">
        {children}
      </div>
    </MenuContext.Provider>
  );
}

export function MenuTrigger({ children }: { children?: ReactNode }) {
  const { open, setOpen } = useMenuContext();

  return (
    <div data-slot="menu-trigger" onClick={() => setOpen(!open)}>
      {children}
    </div>
  );
}

export function MenuPortal({ children }: { children?: ReactNode }) {
  return <>{children}</>;
}

export function MenuPositioner({ children }: { children?: ReactNode }) {
  return <>{children}</>;
}

export function MenuPopup({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  const { open } = useMenuContext();
  if (!open) {
    return null;
  }

  return (
    <div
      data-slot="menu-popup"
      className={cn("absolute right-0 z-50 mt-1 min-w-40 rounded-xl border border-border bg-surface-raised p-1 shadow-lg", className)}
      {...props}
    >
      {children}
    </div>
  );
}

export function MenuItem({ className, children, onClick, ...props }: HTMLAttributes<HTMLButtonElement>) {
  const { setOpen } = useMenuContext();

  return (
    <button
      data-slot="menu-item"
      type="button"
      className={cn("block w-full cursor-pointer rounded-lg px-2 py-1.5 text-left text-sm text-foreground-subtle hover:bg-muted", className)}
      onClick={(event) => {
        onClick?.(event);
        setOpen(false);
      }}
      {...props}
    >
      {children}
    </button>
  );
}
