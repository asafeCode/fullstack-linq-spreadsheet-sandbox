import { createContext, useContext, useMemo, useState, type HTMLAttributes, type ReactNode } from "react";
import { cn } from "../utils/format";

interface TabsContextValue {
  activeValue: string;
  setActiveValue: (value: string) => void;
}

const TabsContext = createContext<TabsContextValue | null>(null);

function useTabsContext(): TabsContextValue {
  const context = useContext(TabsContext);
  if (!context) {
    throw new Error("Tabs components must be used inside TabsRoot.");
  }

  return context;
}

interface TabsRootProps extends HTMLAttributes<HTMLDivElement> {
  defaultValue?: string;
  value?: string;
  onValueChange?: (value: string) => void;
}

export function TabsRoot({ className, defaultValue, value, onValueChange, children, ...props }: TabsRootProps) {
  const [internalValue, setInternalValue] = useState(defaultValue ?? "");
  const activeValue = value ?? internalValue;

  const context = useMemo<TabsContextValue>(
    () => ({
      activeValue,
      setActiveValue: (nextValue: string) => {
        setInternalValue(nextValue);
        onValueChange?.(nextValue);
      }
    }),
    [activeValue, onValueChange]
  );

  return (
    <TabsContext.Provider value={context}>
      <div data-slot="tabs-root" className={cn("flex flex-col gap-3", className)} {...props}>
        {children}
      </div>
    </TabsContext.Provider>
  );
}

export function TabsList({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div data-slot="tabs-list" className={cn("inline-flex w-fit rounded-xl border border-border bg-surface p-1", className)} {...props} />;
}

interface TabsTriggerProps extends HTMLAttributes<HTMLButtonElement> {
  value: string;
}

export function TabsTrigger({ className, value, children, ...props }: TabsTriggerProps) {
  const { activeValue, setActiveValue } = useTabsContext();
  const selected = activeValue === value;

  return (
    <button
      data-slot="tabs-trigger"
      data-selected={selected}
      type="button"
      className={cn(
        "rounded-lg px-3 py-1.5 text-sm text-foreground-subtle transition data-[selected=true]:bg-surface-raised data-[selected=true]:text-foreground",
        className
      )}
      onClick={() => setActiveValue(value)}
      {...props}
    >
      {children}
    </button>
  );
}

interface TabsPanelProps extends HTMLAttributes<HTMLDivElement> {
  value: string;
}

export function TabsPanel({ className, value, children, ...props }: TabsPanelProps) {
  const { activeValue } = useTabsContext();

  if (activeValue !== value) {
    return null;
  }

  return (
    <div data-slot="tabs-panel" className={cn("min-h-0", className)} {...props}>
      {children as ReactNode}
    </div>
  );
}
