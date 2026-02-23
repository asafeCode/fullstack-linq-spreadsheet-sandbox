import type { HTMLAttributes } from "react";
import { AlertTriangle, CheckCircle2, Info } from "lucide-react";
import { tv } from "tailwind-variants";
import { cn } from "../utils/format";

const toneStyles = tv({
  base: "flex items-start gap-2 rounded-2xl border px-4 py-3 text-sm",
  variants: {
    tone: {
      info: "border-border bg-muted text-foreground-subtle",
      success: "border-emerald-200 bg-emerald-50 text-emerald-700",
      error: "border-destructive/30 bg-destructive/10 text-destructive"
    }
  },
  defaultVariants: {
    tone: "info"
  }
});

export interface ToastProps extends HTMLAttributes<HTMLDivElement> {
  tone?: "info" | "success" | "error";
}

export function Toast({ tone, className, children, ...props }: ToastProps) {
  const icon = tone === "success" ? <CheckCircle2 className="mt-0.5 h-4 w-4" /> : tone === "error" ? <AlertTriangle className="mt-0.5 h-4 w-4" /> : <Info className="mt-0.5 h-4 w-4" />;

  return (
    <div data-slot="toast" className={cn(toneStyles({ tone }), className)} {...props}>
      {icon}
      <div>{children}</div>
    </div>
  );
}

