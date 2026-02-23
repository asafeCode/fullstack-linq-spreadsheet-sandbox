export function normalizeCompareText(value: unknown): string {
  if (value == null) return "";

  return String(value)
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/\s+/g, "")
    .replace(/[^a-z0-9]/g, "");
}

export function equalsNormalizedText(a: unknown, b: unknown): boolean {
  return normalizeCompareText(a) === normalizeCompareText(b);
}
