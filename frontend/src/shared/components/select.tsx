import { ChevronDown } from "lucide-react";
import {
  Children,
  createContext,
  useEffect,
  useContext,
  isValidElement,
  useMemo,
  useRef,
  useState,
  type HTMLAttributes,
  type RefObject,
  type ReactNode
} from "react";
import { createPortal } from "react-dom";
import { cn } from "../utils/format";

interface SelectContextValue {
  value: string;
  setValue: (value: string) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
  triggerRef: RefObject<HTMLButtonElement | null>;
  query: string;
  setQuery: (value: string) => void;
}

const SelectContext = createContext<SelectContextValue | null>(null);

function useSelectContext(): SelectContextValue {
  const context = useContext(SelectContext);
  if (!context) {
    throw new Error("Select components must be used inside SelectRoot.");
  }

  return context;
}

interface SelectRootProps extends HTMLAttributes<HTMLDivElement> {
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
}

export function SelectRoot({ value, defaultValue = "", onValueChange, children, ...props }: SelectRootProps) {
  const [internalValue, setInternalValue] = useState(defaultValue);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const currentValue = value ?? internalValue;

  const context = useMemo<SelectContextValue>(
    () => ({
      value: currentValue,
      setValue: (nextValue: string) => {
        setInternalValue(nextValue);
        onValueChange?.(nextValue);
        setQuery("");
        setOpen(false);
      },
      open,
      setOpen,
      triggerRef,
      query,
      setQuery
    }),
    [currentValue, onValueChange, open, triggerRef, query]
  );

  useEffect(() => {
    if (!open && query.length > 0) {
      setQuery("");
    }
  }, [open, query]);

  return (
    <SelectContext.Provider value={context}>
      <div data-slot="select-root" className="relative inline-block" {...props}>
        {children}
      </div>
    </SelectContext.Provider>
  );
}

interface SelectTriggerProps extends HTMLAttributes<HTMLButtonElement> {
  children?: ReactNode;
}

export function SelectTrigger({ className, children, ...props }: SelectTriggerProps) {
  const { open, setOpen, triggerRef } = useSelectContext();

  return (
    <button
      ref={triggerRef}
      data-slot="select-trigger"
      type="button"
      className={cn(
        "inline-flex h-10 min-w-28 items-center justify-between gap-2 rounded-xl border border-input bg-surface px-3 text-sm text-foreground shadow-sm",
        className
      )}
      onClick={() => setOpen(!open)}
      {...props}
    >
      {children}
      <ChevronDown className="h-4 w-4 text-muted-foreground" />
    </button>
  );
}

export function SelectPortal({ children }: { children?: ReactNode }) {
  return <>{children}</>;
}

export function SelectPositioner({ children }: { children?: ReactNode }) {
  return <>{children}</>;
}

export function SelectPopup({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  const { open, setOpen, triggerRef, query, setQuery } = useSelectContext();
  const popupRef = useRef<HTMLDivElement | null>(null);
  const normalizedQuery = query.trim().toLowerCase();

  const filteredItems = useMemo(() => {
    return Children.toArray(children).filter((node) => {
      if (!isValidElement(node)) {
        return true;
      }

      const props = node.props as { value?: string; searchValue?: string; children?: ReactNode };
      const rawLabel = typeof props.searchValue === "string"
        ? props.searchValue
        : typeof props.children === "string"
          ? props.children
          : props.value ?? "";
      const searchableText = String(rawLabel).toLowerCase();

      if (normalizedQuery.length === 0) {
        return true;
      }

      return searchableText.includes(normalizedQuery);
    });
  }, [children, normalizedQuery]);

  const allowScroll = filteredItems.length > 20;

  useEffect(() => {
    if (!open) {
      return;
    }

    const onPointerDown = (event: MouseEvent): void => {
      const target = event.target as Node | null;
      if (!target) {
        return;
      }

      if (triggerRef.current?.contains(target)) {
        return;
      }

      if (popupRef.current?.contains(target)) {
        return;
      }

      setOpen(false);
    };

    const onEscape = (event: KeyboardEvent): void => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onEscape);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onEscape);
    };
  }, [open, setOpen, triggerRef]);

  if (!open) {
    return null;
  }

  const rect = triggerRef.current?.getBoundingClientRect();
  const top = rect ? rect.bottom + 6 + window.scrollY : window.scrollY;
  const left = rect ? rect.left + window.scrollX : window.scrollX;
  const minWidth = rect ? Math.max(rect.width, 320) : 320;

  if (typeof document === "undefined") {
    return null;
  }

  return createPortal(
    <div
      ref={popupRef}
      data-slot="select-popup"
      className={cn("absolute z-[100] w-max rounded-xl border border-border bg-surface-raised p-1 shadow-lg", className)}
      style={{ top, left, minWidth, maxWidth: 640 }}
      {...props}
    >
      <div className="p-1">
        <input
          type="text"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Buscar..."
          className="h-8 w-full rounded-lg border border-input bg-surface px-2 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
      </div>
      <div
        className={cn("pl-2 pr-1 pb-1", allowScroll ? "max-h-[680px] overflow-y-auto" : "overflow-hidden")}
      >
        {filteredItems}
        {filteredItems.length === 0 ? (
          <div className="px-2 py-2 text-sm text-muted-foreground">Nenhum resultado.</div>
        ) : null}
      </div>
    </div>,
    document.body
  );
}

interface SelectItemProps extends HTMLAttributes<HTMLButtonElement> {
  value: string;
  searchValue?: string;
}

export function SelectItem({ className, value, searchValue, children, ...props }: SelectItemProps) {
  const { value: currentValue, setValue } = useSelectContext();
  const selected = currentValue === value;
  void searchValue;

  return (
    <button
      data-slot="select-item"
      data-selected={selected}
      type="button"
      className={cn(
        "block w-full cursor-pointer overflow-hidden text-ellipsis whitespace-nowrap rounded-lg px-2 py-1.5 text-left text-sm text-foreground-subtle data-[selected=true]:bg-muted data-[selected=true]:text-foreground",
        className
      )}
      onClick={() => setValue(value)}
      {...props}
    >
      {children}
    </button>
  );
}

export function SelectValue() {
  const { value } = useSelectContext();
  return <span data-slot="select-value">{value}</span>;
}


