import { defineConfig, devices } from '@playwright/test';

/**
 * E2E config. Boots BOTH the backend API and the Angular dev server, then runs the
 * operator-journey tests against the SPA (which proxies /api to the backend).
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  // Generous so the dev server can compile a lazy route on first navigation.
  expect: { timeout: 15_000 },
  fullyParallel: false,
  retries: 1,
  reporter: [['list'], ['html', { outputFolder: 'playwright-report', open: 'never' }]],
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --project ../backend/src/Nrs.Api/Nrs.Api.csproj',
      url: 'http://localhost:5000/swagger/v1/swagger.json',
      reuseExistingServer: true,
      // Generous for CI cold-starts (restore + build + boot).
      timeout: 240_000,
    },
    {
      command: 'npm start',
      url: 'http://localhost:4200',
      reuseExistingServer: true,
      timeout: 240_000,
    },
  ],
});
