import { defineConfig, devices } from '@playwright/test';

const externalBaseUrl = process.env['PLAYWRIGHT_BASE_URL'];

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  retries: process.env['CI'] ? 2 : 0,
  reporter: [['html', { open: 'never' }]],
  use: { baseURL: externalBaseUrl ?? 'http://127.0.0.1:4200', trace: 'on-first-retry' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], channel: 'chrome' } }],
  webServer: externalBaseUrl ? undefined : {
    command: 'npm start -- --host 127.0.0.1 --port 4200',
    url: 'http://127.0.0.1:4200/login',
    reuseExistingServer: !process.env['CI'],
    timeout: 120000
  }
});
