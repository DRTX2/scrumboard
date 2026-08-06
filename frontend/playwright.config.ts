import { defineConfig, devices } from '@playwright/test';

const externalBaseUrl = process.env['PLAYWRIGHT_BASE_URL'];
const demoBaseUrl = process.env['E2E_DEMO'] === 'true' ? 'http://localhost:8080' : undefined;
const baseURL = externalBaseUrl ?? demoBaseUrl ?? 'http://127.0.0.1:4200';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  workers: process.env['E2E_DEMO'] === 'true' ? 1 : undefined,
  retries: process.env['CI'] ? 2 : 0,
  reporter: [['html', { open: 'never' }]],
  expect: { timeout: 10000 },
  use: { baseURL, screenshot: 'only-on-failure', trace: 'on-first-retry' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], channel: 'chrome' } }],
  webServer: externalBaseUrl || demoBaseUrl ? undefined : {
    command: 'npm start -- --host 127.0.0.1 --port 4200',
    url: 'http://127.0.0.1:4200/login',
    reuseExistingServer: !process.env['CI'],
    timeout: 120000
  }
});
