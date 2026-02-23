import { ChevronDown } from "lucide-react";
import {
  createContext,
  useContext,
  useMemo,
  useState,
  type HTMLAttributes,
  type ReactNode
} from "react";
import { cn } from "../utils/format";

interface SelectContextValue {
  value: string;
  setValue: (value: string) => void;
  open: boolean;
  setOpen: (open: boolean) => void;
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
  const currentValue = value ?? internalValue;

  const context = useMemo<SelectContextValue>(
    () => ({
      value: currentValue,
      setValue: (nextValue: string) => {
        setInternalValue(nextValue);
        onValueChange?.(nextValue);
        setOpen(false);
      },
      open,
      setOpen
    }),
    [currentValue, onValueChange, open]
  );

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
  const { open, setOpen } = useSelectContext();

  return (
    <button
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
  const { open } = useSelectContext();
  if (!open) {
    return null;
  }

  return (
    <div
      data-slot="select-popup"
      className={cn("absolute right-0 z-50 mt-1 min-w-32 rounded-xl border border-border bg-surface-raised p-1 shadow-lg", className)}
      {...props}
    >
      {children}
    </div>
  );
}

interface SelectItemProps extends HTMLAttributes<HTMLButtonElement> {
  value: string;
}

export function SelectItem({ className, value, children, ...props }: SelectItemProps) {
  const { value: currentValue, setValue } = useSelectContext();
  const selected = currentValue === value;

  return (
    <button
      data-slot="select-item"
      data-selected={selected}
      type="button"
      className={cn(
        "block w-full cursor-pointer rounded-lg px-2 py-1.5 text-left text-sm text-foreground-subtle data-[selected=true]:bg-muted data-[selected=true]:text-foreground",
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

