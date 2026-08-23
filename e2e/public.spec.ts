import { test, expect } from '@playwright/test';

async function openFeature(page: any, name: string) {
  const hamburger = page.getByRole('button').filter({ has: page.locator('svg') }).first();
  const navButton = page.getByRole('button', { name });
  if (!(await navButton.isVisible().catch(() => false))) await hamburger.click();
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

test('PWA install diagnostic remains visible on an uninstalled browser', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Cài Khu phố 45')).toBeVisible();
  await expect(page.getByRole('button', { name: /Cài ứng dụng|Kiểm tra cài đặt/ })).toBeVisible();
});

test('simulated beforeinstallprompt invokes the application install prompt handler', async ({ page }) => {
  await page.goto('/');
  await page.evaluate(() => {
    const evt: any = new Event('beforeinstallprompt');
    evt.prompt = async () => { (window as any).__kp45PromptCalled = true; };
    evt.userChoice = Promise.resolve({ outcome: 'accepted', platform: 'web' });
    window.dispatchEvent(evt);
  });
  const install = page.getByRole('button', { name: 'Cài ứng dụng' });
  await expect(install).toBeVisible();
  await install.click();
  await expect.poll(() => page.evaluate(() => Boolean((window as any).__kp45PromptCalled))).toBe(true);
});

test('standalone mode hides the install section after simulated installation', async ({ page }) => {
  await page.addInitScript(() => {
    const original = window.matchMedia.bind(window);
    window.matchMedia = ((query: string) => {
      if (query === '(display-mode: standalone)') return {
        matches: true, media: query, onchange: null,
        addListener() {}, removeListener() {}, addEventListener() {}, removeEventListener() {},
        dispatchEvent() { return true; }
      } as MediaQueryList;
      return original(query);
    }) as typeof window.matchMedia;
  });
  await page.goto('/');
  await expect(page.getByText('Cài Khu phố 45')).toHaveCount(0);
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
