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
  await expect(page.locator('#pageTitle')).toContainText(/nhân khẩu/i);
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

test('OCR nhiều tệp: chọn trường, sửa giá trị và xác nhận riêng', async ({page},testInfo) => {
  await enterApp(page);
  const mobile=testInfo.project.name.startsWith('mobile');
  await (mobile?page.locator('[data-mobile-page="import"]'):page.locator('[data-page="import"]')).click();
  await expect(page.locator('#fileIn')).toBeVisible();
  let previews=0;
  await page.route('**/api/import/preview',async route=>{
    previews++;
    await route.fulfill({json:{ocr_review:true,headers:['Họ tên OCR','Số CCCD nhận dạng'],
      rows:[{'Họ tên OCR':previews===1?'Nguyễn A':'Trần B','Số CCCD nhận dạng':''}],
      preview:[],count:1,mapping:{'Họ tên OCR':'full_name'},raw_text:'Họ tên: '+(previews===1?'Nguyễn A':'Trần B'),warnings:['Đối chiếu ảnh gốc.']}});
  });
  const files=[1,2].map(i=>({name:`scan-${i}.png`,mimeType:'image/png',buffer:Buffer.from([137,80,78,71,13,10,26,10])}));
  await page.locator('#fileIn').setInputFiles(files);
  await page.locator('#previewFile').click();
  await expect(page.locator('.ocrValue[data-field="full_name"]')).toHaveValue('Nguyễn A');
  await page.locator('.ocrValue[data-field="full_name"]').fill('Nguyễn A đã sửa');
  await page.locator('#ocrTeam').fill('2');
  let committed:any;
  await page.route('**/api/import/commit',async route=>{
    committed=route.request().postDataJSON();
    await route.fulfill({json:{inserted:1,invalid:0,duplicates:0,errors:[]}});
  });
  page.once('dialog',dialog=>dialog.accept());
  await page.locator('#commitOcr').click();
  await expect(page.locator('.ocrValue[data-field="full_name"]')).toHaveValue('Trần B');
  expect(committed.rows).toEqual([{'Tổ':'2','Họ tên OCR':'Nguyễn A đã sửa'}]);
  expect(committed.selected).toEqual(['team_id','full_name']);
  await expect(page.locator('#importResult')).toContainText('còn 0 tệp');
});
