import type { HTMLAttributes } from "react";
import { tv } from "tailwind-variants";
import { cn } from "../utils/format";

const badgeStyles = tv({
  base: "inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium",
  variants: {
    variant: {
      success: "border-emerald-300 bg-emerald-50 text-emerald-700",
      warn: "border-amber-300 bg-amber-50 text-amber-700",
      error: "border-destructive/30 bg-destructive/10 text-destructive",
      info: "border-border bg-muted text-foreground-subtle"
    }
  },
  defaultVariants: {
    variant: "info"
  }
});

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: "success" | "warn" | "error" | "info";
}

export function Badge({ className, variant, ...props }: BadgeProps) {
  return <span data-slot="badge" className={cn(badgeStyles({ variant }), className)} {...props} />;
}

