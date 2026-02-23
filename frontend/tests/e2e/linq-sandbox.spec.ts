import { expect, test } from "@playwright/test";
import { mkdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const MOCK_DIR = join(process.cwd(), "tests", "e2e", ".tmp");
const MOCK_FILE = join(MOCK_DIR, "usuarios_ativos_serra_mock_5mb.csv");
const MOCK_FILE2 = join(MOCK_DIR, "sheet2_small.csv");
const TARGET_BYTES = 5 * 1024 * 1024;

function createMockCsv5Mb(): string {
  mkdirSync(MOCK_DIR, { recursive: true });

  const header = "Nome,Tipo,Cidade,Status,Certificados_Contratados,Score,Observacao\n";
  const note = "Detalhe para teste E2E de upload e filtros LINQ.".repeat(6);

  const lines: string[] = [header];
  let size = Buffer.byteLength(header, "utf8");
  let i = 1;

  while (size < TARGET_BYTES) {
    const tipo = i % 3 === 0 ? "Premium" : i % 2 === 0 ? "Basico" : "Trial";
    const cidade = i % 4 === 0 ? "Serra" : i % 4 === 1 ? "Vitoria" : i % 4 === 2 ? "Cariacica" : "VilaVelha";
    const status = i % 5 === 0 ? "Inativo" : "Ativo";
    const cert = i % 7 === 0 ? "" : i % 3 === 0 ? "Azure;AWS" : "Scrum";
    const score = (i % 100) + 1;
    const row = `Usuario_${i},${tipo},${cidade},${status},${cert},${score},${note}\n`;

    lines.push(row);
    size += Buffer.byteLength(row, "utf8");
    i += 1;
  }

  writeFileSync(MOCK_FILE, lines.join(""), "utf8");
  return MOCK_FILE;
}

test.describe("LINQ Sandbox E2E Core", () => {
  test.beforeAll(() => {
    createMockCsv5Mb();
    writeFileSync(MOCK_FILE2, "Nome,cs_responsavel\nAna,AnaCS\nBruno,\n", "utf8");
    const sizeMb = statSync(MOCK_FILE).size / (1024 * 1024);
    if (sizeMb < 4.5 || sizeMb > 5.5) {
      throw new Error(`Mock file size out of expected range: ${sizeMb.toFixed(2)} MB`);
    }
  });

  test.afterAll(() => {
    rmSync(MOCK_DIR, { recursive: true, force: true });
  });

  async function uploadAndWait(page: import("@playwright/test").Page): Promise<void> {
    await page.locator("input[type='file']").first().setInputFiles(MOCK_FILE);
    await expect(page.locator("[data-slot='spreadsheet-viewer']")).toBeVisible({ timeout: 5000 });
    await expect(page.locator("[data-slot='spreadsheet-viewer']")).toContainText("rows");
  }

  test("upload ~5MB should render in up to 5s", { timeout: 5000 }, async ({ page }) => {
    const start = Date.now();
    await page.goto("/");
    await uploadAndWait(page);
    const elapsed = Date.now() - start;
    expect(elapsed).toBeLessThanOrEqual(5000);
  });

  test("upload two sheets should show both in preview", { timeout: 8000 }, async ({ page }) => {
    await page.goto("/");
    const inputs = page.locator("input[type='file']");
    await inputs.nth(0).setInputFiles(MOCK_FILE);
    await inputs.nth(1).setInputFiles(MOCK_FILE2);

    const viewer = page.locator("[data-slot='spreadsheet-viewer']");
    await expect(viewer).toBeVisible();
    await expect(viewer.getByRole("button", { name: "sheet1" })).toBeVisible();
    await expect(viewer.getByRole("button", { name: "sheet2" })).toBeVisible();
  });
});
