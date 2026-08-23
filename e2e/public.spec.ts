import { test, expect } from '@playwright/test';

async function openFeature(page: any, name: string) {
  const hamburger = page.getByRole('button').filter({ has: page.locator('svg') }).first();
  const navButton = page.getByRole('button', { name });
  if (!(await navButton.isVisible().catch(() => false))) {
    await hamburger.click();
  }
  await navButton.click();
}

test('production home loads', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Khu phố 45/i);
  await expect(page.locator('body')).toContainText('Khu phố 45');
});

test('anonymous user is blocked from submitting a report through the real UI', async ({ page }) => {
  await page.goto('/');
  await openFeature(page, 'Phản ánh');
  await page.getByRole('button', { name: 'Gửi phản ánh' }).click();
  await expect(page.getByText('Cần đăng nhập để gửi phản ánh.')).toBeVisible();
  await expect(page.getByText('Đăng nhập để xem phản ánh của bạn.')).toBeVisible();
});

test('official service search works through the real UI', async ({ page }) => {
  await page.goto('/');
  await openFeature(page, 'Dịch vụ công');
  await page.getByPlaceholder('Tìm dịch vụ…').fill('BHYT');
  await page.getByRole('button', { name: 'Tìm' }).click();
  await expect(page.locator('.results')).toContainText(/Bảo hiểm|BHXH/i);
});

test('PWA manifest and service worker are available', async ({ request }) => {
  const manifest = await request.get('/manifest.webmanifest');
  expect(manifest.ok()).toBeTruthy();
  expect(manifest.headers()['content-type']).toMatch(/json|manifest/i);
  const sw = await request.get('/service-worker.js');
  expect(sw.ok()).toBeTruthy();
  expect(sw.headers()['content-type']).toMatch(/javascript|text\/plain/i);
});
