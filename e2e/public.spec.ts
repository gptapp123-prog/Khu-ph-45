import { test, expect } from '@playwright/test';

async function openPublicFeature(page: any, name: string) {
  const grid = page.locator('.digitalHeroLinks');
  await expect(grid).toBeVisible();
  await grid.getByRole('button', { name: new RegExp(name, 'i') }).click();
}

test('redesigned home has three ordered public actions', async ({ page }) => {
  await page.goto('/');
  const actions = page.locator('.digitalHeroLinks button');
  await expect(actions).toHaveCount(3);
  await expect(actions.nth(0)).toContainText('Bản tin');
  await expect(actions.nth(1)).toContainText('Tra cứu');
  await expect(actions.nth(2)).toContainText('Phản ánh');
  await expect(page.locator('.digitalHeroLinks')).not.toContainText('Tìm trụ sở');
});

test('office lookup sits under neighborhood address', async ({ page }) => {
  await page.goto('/');
  const office = page.locator('.officeMini');
  await expect(office).toContainText('1386 Tỉnh lộ 10, phường Bình Tân, TP.HCM');
  await expect(office.getByRole('link', { name: /Tìm trụ sở/i })).toBeVisible();
  const href = await office.getByRole('link', { name: /Tìm trụ sở/i }).getAttribute('href');
  expect(href).toContain('google.com/maps');
});

test('population summary remains visible and card-based', async ({ page }) => {
  await page.goto('/');
  const stats = page.locator('.stats');
  await expect(stats).toBeVisible();
  await expect(stats.locator('.statTile')).toHaveCount(4);
  await expect(stats).toContainText('Tổng số dân');
  await expect(stats).toContainText('Hộ dân');
});

test('sign in entry has no lock icon and account panel opens', async ({ page }) => {
  await page.goto('/');
  const login = page.getByRole('button', { name: 'Đăng nhập / Đăng ký' });
  await expect(login).toBeVisible();
  await expect(login.locator('svg')).toHaveCount(0);
  await login.click();
  const tabs = page.locator('.accountTabs');
  await expect(tabs.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  await expect(tabs.getByRole('button', { name: 'Tạo tài khoản', exact: true })).toBeVisible();
});

test('official free-text service search still works', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  await page.getByPlaceholder(/tôi cần xác nhận cư trú/i).fill('đăng ký tạm trú');
  await page.getByRole('button', { name: 'Tra cứu', exact: true }).click();
  const links = page.locator('.serviceResults a');
  await expect(links.first()).toBeVisible({ timeout: 10000 });
  const count = await links.count();
  for (let i = 0; i < count; i++) {
    const href = await links.nth(i).getAttribute('href');
    expect(href).toMatch(/^https:\/\/([a-z0-9.-]+\.)?gov\.vn\//i);
  }
});

test('report form retains required identity fields', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Phản ánh');
  await expect(page.getByPlaceholder('Nhập họ và tên')).toHaveAttribute('required', '');
  await expect(page.getByPlaceholder('Ví dụ: 0901234567')).toHaveAttribute('required', '');
});

test('service worker excludes API and auth routes from cache', async ({ request }) => {
  const sw = await request.get('/service-worker.js');
  expect(sw.ok()).toBeTruthy();
  const source = await sw.text();
  expect(source).toContain("u.pathname.startsWith('/api/')");
  expect(source).toContain("u.pathname.startsWith('/__appdeploy/')");
});
