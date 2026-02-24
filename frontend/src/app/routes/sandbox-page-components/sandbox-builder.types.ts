export type FilterOperator = "equals" | "contains" | "startsWith" | "endsWith" | "isEmpty";

export interface SimpleFilter {
  id: string;
  column: string;
  operator: FilterOperator;
  value: string;
  negate: boolean;
  joinWith: "AND" | "OR";
}

export interface ProjectionField {
  id: string;
  column: string;
  alias: string;
}
