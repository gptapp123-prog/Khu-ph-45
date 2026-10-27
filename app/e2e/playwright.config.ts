import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: /quan-ly-khu-pho-7-5\.spec\.ts/,
  timeout: 60000,
  expect: { timeout: 10000 },
  retries: 1,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../../playwright-report-qlkp', open: 'never' }]],
  use: {
    baseURL: process.env.QLKP_BASE_URL || 'http://127.0.0.1:8765',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    { name: 'desktop-1280', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } },
    { name: 'mobile-360', use: { ...devices['Pixel 7'], viewport: { width: 360, height: 800 } } }
  ]
});
