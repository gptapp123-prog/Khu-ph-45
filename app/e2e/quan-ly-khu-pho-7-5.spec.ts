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
  await expect(page.locator('.ocrValue[data-index="0"]')).toHaveValue('Nguyễn A');
  await page.locator('.ocrValue[data-index="0"]').fill('Nguyễn A đã sửa');
  await page.locator('#ocrTeam').fill('2');
  let committed:any;
  await page.route('**/api/import/commit',async route=>{
    committed=route.request().postDataJSON();
    await route.fulfill({json:{inserted:1,invalid:0,duplicates:0,errors:[]}});
  });
  page.once('dialog',dialog=>dialog.accept());
  await page.locator('#commitOcr').click();
  await expect(page.locator('.ocrValue[data-index="0"]')).toHaveValue('Trần B');
  expect(committed.rows).toEqual([{'Tổ':'2','Họ tên OCR':'Nguyễn A đã sửa'}]);
  expect(committed.selected).toEqual(['team_id','full_name']);
  await expect(page.locator('#importResult')).toContainText('còn 0 tệp');
});

test('bảng OCR có mẫu xe, chọn chế độ ảnh và phóng to để đối chiếu', async ({page},testInfo) => {
  test.skip(testInfo.project.name.startsWith('mobile'), 'Xuất OCR chỉ dùng tại PC chính');
  await enterApp(page);
  await page.locator('[data-page="import"]').click();
  await page.route('**/api/ocr/templates',async route=>{
    if(route.request().method()==='GET')await route.fulfill({json:{items:[{id:'test-template',name:'Giấy đăng ký xe',fields:[{label:'Số khung',column:'Số khung',type:'identifier',aliases:[],expected_length:17}]}]}});
    else await route.fulfill({json:{id:'test-template'}});
  });
  await page.locator('#openConfiguredOcr').click();
  page.once('dialog',dialog=>dialog.accept());
  await page.locator('#ocrVehiclePreset').click();
  await expect(page.locator('.ocrFieldRow')).toHaveCount(9);
  await expect(page.locator('.ocrLabel').nth(3)).toHaveValue('Số khung');
  await page.locator('#ocrTemplateSelect').selectOption('test-template');
  await page.locator('#ocrImageMode').selectOption('enhanced');
  await page.route('**/api/import/preview?*',async route=>{
    expect(route.request().url()).toContain('ocr_mode=enhanced');
    await route.fulfill({json:{ocr_review:true,raw_text:'Số khung: RL4JW3EM3M0000000',warnings:[]}});
  });
  await page.route('**/api/ocr/extract',async route=>await route.fulfill({json:{fields:[{label:'Số khung',column:'Số khung',type:'identifier',aliases:[],expected_length:17}],values:{'Số khung':'RL4JW3EM3M0000000'},warnings:[]}}));
  await page.locator('#ocrFiles').setInputFiles({name:'scan.png',mimeType:'image/png',buffer:Buffer.from([137,80,78,71,13,10,26,10])});
  await page.locator('#ocrReadFiles').click();
  await expect(page.locator('.ocrConfirmed')).toHaveValue('RL4JW3EM3M0000000');
  await page.locator('.ocrZoom').evaluate((el:HTMLInputElement)=>{el.value='180';el.dispatchEvent(new Event('input',{bubbles:true}));});
  await expect(page.locator('.ocrSourceImage')).toHaveAttribute('style',/width: 180%/);
  await page.locator('.ocrBrightness').evaluate((el:HTMLInputElement)=>{el.value='130';el.dispatchEvent(new Event('input',{bubbles:true}));});
  await expect(page.locator('.ocrSourceImage')).toHaveCSS('filter','brightness(1.3)');
});
