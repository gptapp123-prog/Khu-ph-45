import { test, expect } from '@playwright/test';

async function openPublicFeature(page: any, name: string) {
  const grid = page.locator('.digitalHeroLinks');
  await expect(grid).toBeVisible();
  await grid.getByRole('button', { name: new RegExp(name, 'i') }).click();
}

async function search(page: any, keyword: string) {
  const input = page.getByPlaceholder(/Ví dụ: tôi cần xác nhận cư trú/i);
  await input.fill(keyword);
  const started = Date.now();
  await page.getByRole('button', { name: 'Tra cứu', exact: true }).click();
  await expect(page.locator('.serviceResults a').first()).toBeVisible({ timeout: 10000 });
  return Date.now() - started;
}

function expectOfficial(url: string) {
  const u = new URL(url);
  expect(u.protocol).toBe('https:');
  expect(u.hostname === 'gov.vn' || u.hostname.endsWith('.gov.vn')).toBeTruthy();
}

test('approved home layout has four unique public actions and exact office address', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle(/Khu phố 45/i);
  await expect(page.locator('.digitalHeroLinks').locator('button,a')).toHaveCount(4);
  await expect(page.locator('.spotlight')).toBeVisible();
  await expect(page.locator('.homePeople')).toBeVisible();
  await expect(page.locator('.officePanel')).toContainText('1386 Tỉnh lộ 10, phường Bình Tân, TP.HCM');
});

test('arbitrary service query returns only official authority links', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  await search(page, 'xác nhận cư trú để bổ sung hồ sơ vay vốn');
  const links = page.locator('.serviceResults a');
  expect(await links.count()).toBeGreaterThan(0);
  for (let i = 0; i < await links.count(); i++) expectOfficial((await links.nth(i).getAttribute('href')) || '');
  await expect(page.locator('.searchMeta')).toContainText('nguồn chính thức');
});

test('repeated normal use handles diverse queries without fixed-keyword failures', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  const queries = ['đăng ký tạm trú','hộ chiếu cho trẻ em','đổi giấy phép lái xe','bảo hiểm y tế hộ gia đình','khai sinh trực tuyến','thuế hộ kinh doanh','thủ tục đất đai','hỗ trợ người cao tuổi'];
  const times: number[] = [];
  for (const q of queries) {
    times.push(await search(page, q));
    await expect(page.locator('.serviceResults a').first()).toBeVisible();
  }
  console.log('KP45_SEARCH_LATENCY_MS', JSON.stringify(times));
  expect(Math.max(...times)).toBeLessThan(10000);
  await expect(page.locator('.historyScroll > button')).toHaveCount(8);
});

test('search history survives reload, deduplicates and can be replayed', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  await search(page, 'hộ chiếu');
  await search(page, 'bảo hiểm y tế');
  await search(page, 'hộ chiếu');
  await page.reload();
  await openPublicFeature(page, 'Tra cứu');
  const history = page.locator('.historyScroll > button');
  await expect(history).toHaveCount(2);
  await history.filter({ hasText: 'hộ chiếu' }).click();
  await expect(page.locator('.serviceResults a').first()).toBeVisible({ timeout: 10000 });
});

test('long service and history areas are bounded and scrollable on mobile', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  for (const q of ['cư trú','hộ chiếu','thuế','bảo hiểm','khai sinh','đất đai']) await search(page, q);
  const results = page.locator('.serviceResults');
  const history = page.locator('.historyScroll');
  await expect(results).toBeVisible();
  await expect(history).toBeVisible();
  const resultStyle = await results.evaluate((el: HTMLElement) => ({ overflow: getComputedStyle(el).overflowY, maxHeight: getComputedStyle(el).maxHeight }));
  const historyStyle = await history.evaluate((el: HTMLElement) => ({ overflow: getComputedStyle(el).overflowY, maxHeight: getComputedStyle(el).maxHeight }));
  expect(['auto','scroll']).toContain(resultStyle.overflow);
  expect(['auto','scroll']).toContain(historyStyle.overflow);
  expect(resultStyle.maxHeight).not.toBe('none');
  expect(historyStyle.maxHeight).not.toBe('none');
});

test('clear history does not disable subsequent search', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Tra cứu');
  await search(page, 'khai sinh');
  await page.getByRole('button', { name: 'Xóa lịch sử' }).click();
  await expect(page.locator('.searchHistory')).toHaveCount(0);
  await search(page, 'hộ tịch');
  await expect(page.locator('.serviceResults a').first()).toBeVisible();
});

test('report form keeps mandatory identity validation', async ({ page }) => {
  await page.goto('/');
  await openPublicFeature(page, 'Phản ánh');
  const name = page.getByPlaceholder('Nhập họ và tên');
  const phone = page.getByPlaceholder('Ví dụ: 0901234567');
  await expect(name).toHaveAttribute('required', '');
  await expect(phone).toHaveAttribute('required', '');
});

test('local account authentication entry remains available', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'Đăng nhập / Đăng ký' }).click();
  await expect(page.getByPlaceholder('Tên người dùng / email / số điện thoại')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Tạo tài khoản' })).toBeVisible();
});

test('service worker still isolates app API routes', async ({ request }) => {
  const manifest = await request.get('/manifest.webmanifest');
  expect(manifest.ok()).toBeTruthy();
  const sw = await request.get('/service-worker.js');
  expect(sw.ok()).toBeTruthy();
  const source = await sw.text();
  expect(source).toContain("u.pathname.startsWith('/api/')");
  expect(source).toContain("u.pathname.startsWith('/__appdeploy/')");
});
