import { test, expect } from '@playwright/test';

async function openPublicFeature(page: any, name: string) {
  const grid = page.locator('.digitalHeroLinks');
  await expect(grid).toBeVisible();
  await grid.getByRole('button', { name: new RegExp(name, 'i') }).click();
}

test('approved home layout has four unique public actions, population cards and office lookup below address', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Khu phố 45/i);
  await expect(page.locator('.digitalHero')).toContainText('KHU PHỐ');
  await expect(page.locator('.digitalHero')).toContainText('45');
  await expect(page.locator('.digitalHero')).toContainText('Một chạm tới những việc cần thiết');
  const publicActions = page.locator('.digitalHeroLinks').locator('button');
  await expect(publicActions).toHaveCount(4);
  await expect(page.locator('.digitalHeroLinks')).toContainText('Tra cứu · Dịch vụ công');
  await expect(page.locator('.populationPanel')).toBeVisible();
  await expect(page.locator('.populationTile')).toHaveCount(4);
  await expect(page.locator('.officeMini')).toContainText('1386 Tỉnh lộ 10, phường Bình Tân, TP.HCM');
  await expect(page.locator('.officeMini').getByRole('link', { name: /Tìm trụ sở/i })).toHaveCount(1);
  await expect(page.locator('.officePanel')).toHaveCount(0);
});

test('party screen is minimal and contains no source-note copy', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Đảng viên');
  await expect(page.locator('.partyGrid a')).toHaveCount(4);
  await expect(page.locator('.partyGrid')).toContainText('Sổ tay đảng viên điện tử');
  await expect(page.locator('.partyGrid')).toContainText('Đóng đảng phí');
  await expect(page.locator('.partyGrid')).toContainText('Thủ tục hành chính');
  await expect(page.locator('.partyGrid')).toContainText('Hành chính sự nghiệp');
  await expect(page.locator('.partyScreen')).not.toContainText(/Mở dcs\.vn|Mở nguồn|Mở hệ thống chính thức/i);
  await expect(page.locator('.partyEmblemSvg')).toHaveCount(5);
});

test('report form exposes mandatory reporter name and Vietnam phone fields', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Phản ánh');
  const name = page.getByPlaceholder('Nhập họ và tên');
  const phone = page.getByPlaceholder('Ví dụ: 0901234567');
  await expect(name).toBeVisible();
  await expect(phone).toBeVisible();
  await expect(name).toHaveAttribute('required', '');
  await expect(phone).toHaveAttribute('required', '');
});

test('official service search opens from the single Tra cứu · Dịch vụ công action', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  const input = page.getByPlaceholder(/Ví dụ: tôi cần xác nhận cư trú/i);
  await input.fill('BHYT');
  await page.getByRole('button', { name: 'Tra cứu' }).click();
  await expect(page.locator('.serviceResults')).toBeVisible();
});

test('exactly one top install control is visible before standalone mode', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('.installTop')).toBeVisible();
  await expect(page.locator('.shell div.install')).toBeHidden();
  await expect(page.locator('header').getByRole('button', { name: /Cài KP45/i })).toHaveCount(1);
});

test('account entry opens login and registration UI without exposing protected role icons', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Đăng nhập / Đăng ký', exact: true }).click();
  const tabs = page.locator('.accountTabs');
  await expect(tabs.getByRole('button', { name: 'Đăng nhập', exact: true })).toBeVisible();
  await expect(tabs.getByRole('button', { name: 'Tạo tài khoản', exact: true })).toBeVisible();
  await expect(page.locator('.roleFeatureGrid')).toHaveCount(0);
});
