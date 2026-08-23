import { test, expect } from '@playwright/test';

const enabled = process.env.E2E_AUTH_ENABLED === '1';
const state = 'playwright/.auth/user.json';

test.describe('authenticated production', () => {
  test.skip(!enabled, 'Authenticated browser state is not configured');
  test.use({ storageState: state });

  test('authenticated account loads its profile and private area', async ({ page }) => {
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
});
