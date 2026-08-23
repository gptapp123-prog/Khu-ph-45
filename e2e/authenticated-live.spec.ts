import { test, expect } from '@playwright/test';

const state = 'playwright/.auth/user.json';

test.use({ storageState: state });

test('authenticated account loads profile and private reports area', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: /Mã tài khoản/i })).toBeVisible();
  await page.getByRole('button', { name: 'Phản ánh' }).first().click();
  await expect(page.getByText('Phản ánh của tôi')).toBeVisible();
  await expect(page.getByText('Đăng nhập để xem phản ánh của bạn.')).toHaveCount(0);
});

test('authenticated session survives reload', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: /Mã tài khoản/i })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: /Mã tài khoản/i })).toBeVisible();
});

test('role navigation is least-privilege consistent with the signed-in account', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: /Mã tài khoản/i })).toBeVisible();
  const header = await page.locator('header').innerText();
  const text = header.toLowerCase();
  const isAdmin = text.includes('admin');
  const isLeader = text.includes('leader') || text.includes('bí thư') || text.includes('trưởng khu phố');
  const isGroup = text.includes('group') || text.includes('tổ dân phố');
  if (isAdmin || isLeader) {
    await expect(page.getByRole('button', { name: 'Điều hành' })).toBeVisible();
  } else {
    await expect(page.getByRole('button', { name: 'Điều hành' })).toHaveCount(0);
  }
  if (isGroup) {
    await expect(page.getByRole('button', { name: 'Công việc' })).toBeVisible();
  } else {
    await expect(page.getByRole('button', { name: 'Công việc' })).toHaveCount(0);
  }
});

test('logout removes authenticated controls and private navigation', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: /Mã tài khoản/i })).toBeVisible();
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('button', { name: 'Đăng nhập Google' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Mã tài khoản/i })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Điều hành' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Công việc' })).toHaveCount(0);
});
