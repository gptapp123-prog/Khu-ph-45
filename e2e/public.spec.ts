import { test, expect } from '@playwright/test';

async function openPublicFeature(page: any, name: string) {
  const grid = page.locator('.digitalHeroLinks');
  await expect(grid).toBeVisible();
  await grid.getByRole('button', { name: new RegExp(name, 'i') }).click();
}

test('approved home layout has four unique public actions and exact office address', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Khu phố 45/i);
  await expect(page.locator('.digitalHero')).toContainText('KHU PHỐ');
  await expect(page.locator('.digitalHero')).toContainText('45');
  const publicActions = page.locator('.digitalHeroLinks').locator('button,a');
  await expect(publicActions).toHaveCount(4);
  await expect(page.locator('.featureGrid')).toHaveCount(0);
  await expect(page.locator('.spotlight')).toBeVisible();
  await expect(page.locator('.homePeople')).toBeVisible();
  await expect(page.locator('.officePanel')).toContainText('1386 Tỉnh lộ 10, phường Bình Tân, TP.HCM');
  const officeHref = await page.locator('.officePanel a').first().getAttribute('href');
  expect(decodeURIComponent(officeHref || '')).toContain('1386 Tỉnh lộ 10, phường Bình Tân, TP.HCM');
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
  await page.getByRole('button', { name: 'Gửi phản ánh' }).click();
  expect(await name.evaluate((el: HTMLInputElement) => el.validity.valueMissing)).toBeTruthy();
  expect(await phone.evaluate((el: HTMLInputElement) => el.validity.valueMissing)).toBeTruthy();
  await expect(page.getByText('Cần đăng nhập để gửi phản ánh.')).toHaveCount(0);
});

test('official service search works from the only public Tra cứu action', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
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
