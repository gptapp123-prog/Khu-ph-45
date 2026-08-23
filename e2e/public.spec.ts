import { test, expect } from '@playwright/test';

test('production home loads', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Khu phố 45/i);
  await expect(page.locator('body')).toContainText('Khu phố 45');
});

test('protected reports API rejects anonymous access', async ({ request }) => {
  const response = await request.get('/api/reports');
  const body = await response.text();
  console.log('REPORTS_PROBE', JSON.stringify({ status: response.status(), contentType: response.headers()['content-type'], body: body.slice(0, 300) }));
  expect(response.status()).toBe(401);
});

test('PWA assets are available', async ({ request }) => {
  const manifest = await request.get('/manifest.webmanifest');
  expect(manifest.ok()).toBeTruthy();
  const sw = await request.get('/service-worker.js');
  expect(sw.ok()).toBeTruthy();
});

test('public APIs do not return server errors', async ({ request }) => {
  for (const path of ['/api/news', '/api/people', '/api/stats']) {
    const response = await request.get(path);
    expect(response.status(), path).toBeLessThan(500);
  }
});
