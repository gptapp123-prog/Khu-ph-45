import { test, expect } from '@playwright/test';

const USER='quantri';
const PASSWORD='KhoiTaoGiaoDien_2026!';

async function enterApp(page:any) {
  await page.goto('/');
  await expect(page).toHaveTitle(/Quản lý Khu phố/i);
  const title=page.locator('#authTitle');
  await expect(title).toBeVisible();
  if ((await title.textContent())?.includes('Thiết lập lần đầu')) {
    await page.locator('#username').fill(USER);
    await page.locator('#password').fill(PASSWORD);
    await page.locator('#setupName').fill('Nguyễn Quản Trị E2E');
    await page.locator('#loginButton').click();
    await expect(title).toContainText('Đăng nhập hệ thống');
  }
  await page.locator('#username').fill(USER);
  await page.locator('#password').fill(PASSWORD);
  await page.locator('#loginButton').click();
  await expect(page.locator('#pageTitle')).toContainText('Tổng quan');
}

test('khung giao diện, điều hướng và responsive hoạt động', async ({page},testInfo) => {
  const errors:string[]=[];
  page.on('pageerror',e=>errors.push(String(e)));
  await enterApp(page);
  await expect(page.locator('.module-card')).toHaveCount(6);
  const mobile=testInfo.project.name.startsWith('mobile');
  const featureNav=mobile?page.locator('[data-mobile-page="features"]'):page.locator('[data-page="features"]');
  await featureNav.click();
  await expect(page.locator('#pageTitle')).toContainText('Bảng chức năng');
  await expect(page.locator('#featureTable tbody tr')).toHaveCount(31);
  await page.locator('#featureSearch').fill('F-029');
  await expect(page.locator('#featureTable tbody tr')).toHaveCount(1);
  const residentNav=mobile?page.locator('[data-mobile-page="residents"]'):page.locator('[data-page="residents"]');
  await residentNav.click();
  await expect(page.locator('#pageTitle')).toContainText('Nhân khẩu');
  const overflow=await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(1);
  if (mobile) {
    await expect(page.locator('.bottom-nav')).toBeVisible();
    const sizes=await page.locator('.bottom-nav button').evaluateAll(btns=>btns.map(b=>b.getBoundingClientRect().height));
    expect(Math.min(...sizes)).toBeGreaterThanOrEqual(40);
  }
  await page.screenshot({path:testInfo.outputPath('giao-dien.png'),fullPage:true});
  expect(errors).toEqual([]);
});
