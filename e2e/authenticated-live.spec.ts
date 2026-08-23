import { test, expect } from '@playwright/test';

const username = 'qa_e2e_neon_20260823';
const password = 'Kp45Qa#2026Neon!';

async function login(page: any) {
  await page.goto('/');
  await page.getByRole('button', { name: 'Đăng nhập / Đăng ký' }).click();
  await page.getByPlaceholder('Tên người dùng / email / số điện thoại').fill(username);
  await page.getByPlaceholder('Mật khẩu').fill(password);
  await page.locator('.accountForm button.primary').click();
  await expect(page.locator('header')).toContainText(username);
  await expect(page.getByRole('button', { name: 'Tài khoản' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Đăng xuất' })).toBeVisible();
}

test('Neon-backed resident account signs in and opens private reports area', async ({ page }) => {
  await login(page);
  await page.locator('.digitalHeroLinks').getByRole('button', { name: 'Phản ánh' }).click();
  await expect(page.getByText('Phản ánh của tôi')).toBeVisible();
  await expect(page.getByText('Đăng nhập để xem phản ánh của bạn.')).toHaveCount(0);
});

test('PostgreSQL account session survives reload', async ({ page }) => {
  await login(page);
  await page.reload();
  await expect(page.locator('header')).toContainText(username);
  await expect(page.getByRole('button', { name: 'Đăng xuất' })).toBeVisible();
});

test('resident role remains least privilege after Neon cutover', async ({ page }) => {
  await login(page);
  await expect(page.getByRole('button', { name: 'Điều hành' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Công việc' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Nhân sự' })).toHaveCount(0);
});

test('logout revokes the private session and remains logged out after reload', async ({ page }) => {
  await login(page);
  await page.getByRole('button', { name: 'Đăng xuất' }).click();
  await expect(page.getByRole('button', { name: 'Đăng nhập / Đăng ký' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Đăng nhập / Đăng ký' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Điều hành' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Công việc' })).toHaveCount(0);
});
