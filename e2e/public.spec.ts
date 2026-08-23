// QA regression gate: icon-home navigation, public personnel columns, linked-news layout, single top install control.
import { test, expect } from '@playwright/test';

async function openHomeFeature(page: any, name: string) {
  const grid = page.locator('.featureGrid');
  await expect(grid).toBeVisible();
  await grid.getByRole('button', { name: new RegExp(name, 'i') }).click();
}

test('production home uses icons without a visible menu and shows public personnel columns', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Khu phố 45/i);
  await expect(page.locator('nav')).toBeHidden();
  await expect(page.locator('.hamb')).toBeHidden();
  await expect(page.locator('.featureGrid')).toBeVisible();
  await expect(page.getByText('Lãnh đạo · Cảnh sát khu vực')).toBeVisible();
  await expect(page.getByText('Tổ trưởng Tổ dân phố')).toBeVisible();
  await expect(page.locator('.featureGrid').getByRole('button', { name: /Nhân sự/i })).toHaveCount(0);
});

test('anonymous user is blocked from submitting a report through the home icon', async ({ page }) => {
  await page.goto('/');
  await openHomeFeature(page, 'Phản ánh');
  await page.getByRole('button', { name: 'Gửi phản ánh' }).click();
  await expect(page.getByText('Cần đăng nhập để gửi phản ánh.')).toBeVisible();
  await expect(page.getByText('Đăng nhập để xem phản ánh của bạn.')).toBeVisible();
});

test('official service search works through the home icon', async ({ page }) => {
  await page.goto('/');
  await openHomeFeature(page, 'Dịch vụ công');
  await page.getByPlaceholder('Tìm dịch vụ…').fill('BHYT');
  await page.getByRole('button', { name: 'Tìm' }).click();
  await expect(page.locator('.results')).toContainText(/Bảo hiểm|BHXH/i);
});

test('exactly one top install control is visible before standalone mode', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('.installTop')).toBeVisible();
  await expect(page.locator('.shell div.install')).toBeHidden();
  await expect(page.locator('header').getByRole('button', { name: 'Cài ứng dụng' })).toHaveCount(1);
});

test('service worker isolates AppDeploy auth and API routes', async ({ request }) => {
  const manifest = await request.get('/manifest.webmanifest');
  expect(manifest.ok()).toBeTruthy();
  const sw = await request.get('/service-worker.js');
  expect(sw.ok()).toBeTruthy();
  const source = await sw.text();
  expect(source).toContain("u.pathname.startsWith('/api/')");
  expect(source).toContain("u.pathname.startsWith('/__appdeploy/')");
});

test('Google sign-in button opens the authentication popup', async ({ page }) => {
  await page.goto('/');
  const popupPromise = page.waitForEvent('popup');
  await page.getByRole('button', { name: 'Đăng nhập Google' }).click();
  const popup = await popupPromise;
  await expect(popup).not.toBeNull();
  await popup.close();
});
