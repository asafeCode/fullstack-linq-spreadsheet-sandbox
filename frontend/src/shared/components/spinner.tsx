import { LoaderCircle } from "lucide-react";
import { cn } from "../utils/format";

export interface SpinnerProps {
  className?: string;
}

export function Spinner({ className }: SpinnerProps) {
  return <LoaderCircle data-slot="spinner" className={cn("h-4 w-4 animate-spin", className)} />;
}

