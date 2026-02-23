import type { ButtonHTMLAttributes } from "react";
import { tv } from "tailwind-variants";
import { cn } from "../utils/format";

const buttonStyles = tv({
  base: "inline-flex items-center justify-center gap-2 rounded-2xl border font-medium transition outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50",
  variants: {
    variant: {
      primary: "border-primary bg-primary text-primary-foreground hover:opacity-95",
      secondary: "border-border bg-surface-raised text-foreground hover:bg-muted",
      ghost: "border-transparent bg-transparent text-foreground hover:bg-muted",
      destructive: "border-destructive bg-destructive text-destructive-foreground hover:opacity-95"
    },
    size: {
      sm: "h-8 px-3 text-xs",
      md: "h-10 px-4 text-sm",
      lg: "h-11 px-5 text-sm"
    }
  },
  defaultVariants: {
    variant: "primary",
    size: "md"
  }
});

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: "primary" | "secondary" | "ghost" | "destructive";
  size?: "sm" | "md" | "lg";
}

export function Button({ className, variant, size, ...props }: ButtonProps) {
  return <button data-slot="button" className={cn(buttonStyles({ variant, size }), className)} {...props} />;
}

