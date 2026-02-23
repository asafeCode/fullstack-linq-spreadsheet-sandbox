import { defineConfig } from "@playwright/test";

const frontendBaseUrl = process.env.PLAYWRIGHT_FRONTEND_URL ?? "http://localhost:5173";
const backendBaseUrl = process.env.PLAYWRIGHT_BACKEND_URL ?? "http://localhost:5057";
const backendReadyUrl = process.env.PLAYWRIGHT_BACKEND_READY_URL ?? `${backendBaseUrl}/swagger/index.html`;

export default defineConfig({
  testDir: "./tests/e2e",
  fullyParallel: false,
  workers: 1,
  timeout: 420000,
  expect: {
    timeout: 15000
  },
  reporter: "list",
  use: {
    baseURL: frontendBaseUrl,
    browserName: "chromium",
    channel: "msedge",
    headless: true,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  webServer: [
    {
      command: `dotnet run --project ../backend/src/SpreadsheetFilterApp.Web/SpreadsheetFilterApp.Web.csproj --urls ${backendBaseUrl}`,
      url: backendReadyUrl,
      timeout: 420000,
      reuseExistingServer: false
    },
    {
      command: "npm run dev -- --host localhost --port 5173",
      url: frontendBaseUrl,
      timeout: 420000,
      reuseExistingServer: false,
      env: {
        VITE_API_BASE_URL: backendBaseUrl
      }
    }
  ]
});
