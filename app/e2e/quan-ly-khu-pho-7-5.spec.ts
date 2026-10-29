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

test('QR và OCR khác nhau phải chọn nguồn; QR đơn lẻ vẫn xuất đúng cột', async ({page},testInfo) => {
  test.skip(testInfo.project.name.startsWith('mobile'), 'Xuất QR chỉ dùng tại PC chính');
  await enterApp(page);
  await page.locator('[data-page="import"]').click();
  const fields=[{label:'Số hiệu bằng',column:'Số hiệu bằng',type:'identifier',aliases:[]}];
  await page.route('**/api/ocr/templates',async route=>await route.fulfill({json:{items:[{id:'qr-template',name:'Mã QR',fields}]}}));
  await page.locator('#openConfiguredOcr').click();
  await page.locator('#ocrTemplateSelect').selectOption('qr-template');
  await page.route('**/api/qr/decode?*',async route=>await route.fulfill({json:{codes:[{page:1,text:'Số hiệu bằng: 12345/PT'}],values:{'Số hiệu bằng':'12345/PT'},warnings:[]}}));
  let qrOnly=false;
  await page.route('**/api/import/preview?*',async route=>{
    if(qrOnly)await route.fulfill({status:422,json:{detail:'Không đọc được chữ'}});
    else await route.fulfill({json:{ocr_review:true,raw_text:'Số hiệu bằng: 99999/PT',warnings:[]}});
  });
  await page.route('**/api/ocr/extract',async route=>await route.fulfill({json:{fields,values:{'Số hiệu bằng':qrOnly?'':'99999/PT'},warnings:[]}}));
  await page.locator('#ocrFiles').setInputFiles({name:'scan.png',mimeType:'image/png',buffer:Buffer.from([137,80,78,71,13,10,26,10])});
  await page.locator('#ocrReadFiles').click();
  await expect(page.locator('.ocrConfirmed')).toHaveValue('');
  await expect(page.locator('#ocrResult')).toContainText('OCR và QR khác nhau');
  await page.locator('.ocrUseValue[data-source="qr"]').click();
  await expect(page.locator('.ocrConfirmed')).toHaveValue('12345/PT');
  qrOnly=true;
  await page.locator('#ocrReadFiles').click();
  await expect(page.locator('.ocrConfirmed')).toHaveValue('12345/PT');
  await page.locator('.ocrConfirm').click();
  await expect(page.locator('#ocrExportRows')).toBeEnabled();
});

test('chọn file Excel có sẵn, kiểm tra cột trước khi thêm dòng OCR', async ({page},testInfo) => {
  test.skip(testInfo.project.name.startsWith('mobile'), 'File Excel đích chỉ chọn tại PC chính');
  await enterApp(page);
  await page.locator('[data-page="import"]').click();
  const fields=[{label:'Họ và tên',column:'Họ và tên',type:'text',aliases:[]}];
  await page.route('**/api/ocr/templates',async route=>await route.fulfill({json:{items:[{id:'ocr-template',name:'Danh sách',fields}]}}));
  await page.locator('#openConfiguredOcr').click();
  await page.locator('#ocrTemplateSelect').selectOption('ocr-template');
  let validation=0;
  await page.route('**/api/ocr/target/validate',async route=>{validation++;await route.fulfill({json:{sheet:'Danh sách',existing_rows:2,columns:['Họ và tên']}})});
  await page.locator('#ocrTargetFile').setInputFiles({name:'danh-sach.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('test-xlsx')});
  await expect(page.locator('#ocrTargetStatus')).toContainText('2 dòng hiện có');
  await page.route('**/api/qr/decode?*',async route=>await route.fulfill({json:{codes:[],values:{},warnings:[]}}));
  await page.route('**/api/import/preview?*',async route=>await route.fulfill({json:{ocr_review:true,raw_text:'Họ và tên: Nguyễn A',warnings:[]}}));
  await page.route('**/api/ocr/extract',async route=>await route.fulfill({json:{fields,values:{'Họ và tên':'Nguyễn A'},warnings:[]}}));
  await page.locator('#ocrFiles').setInputFiles({name:'scan.png',mimeType:'image/png',buffer:Buffer.from([137,80,78,71,13,10,26,10])});
  await page.locator('#ocrReadFiles').click();
  await page.locator('.ocrConfirm').click();
  let posted='';
  await page.route('**/api/ocr/export-into-file',async route=>{posted=route.request().postData()||'';await route.fulfill({body:Buffer.from('fake-xlsx'),contentType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'})});
  await page.locator('#ocrExportRows').click();
  await expect.poll(()=>posted).toContain('danh-sach.xlsx');
  expect(posted).toContain('Nguyễn A');
  expect(validation).toBe(1);
});

test('quét tiêu đề, tạo bảng lệnh và chỉ đánh dấu cột cần OCR', async ({page},testInfo) => {
  test.skip(testInfo.project.name.startsWith('mobile'), 'Tạo bảng lệnh chỉ dùng tại PC chính');
  await enterApp(page);
  await page.locator('[data-page="import"]').click();
  let saved:any;
  await page.route('**/api/ocr/templates',async route=>{
    if(route.request().method()==='POST'){saved=route.request().postDataJSON();await route.fulfill({json:{id:'new-template'}})}
    else await route.fulfill({json:{items:[]}});
  });
  await page.locator('#openConfiguredOcr').click();
  await page.route('**/api/ocr/columns/scan',async route=>await route.fulfill({json:{sheets:[{name:'Danh sách',columns:['Mã hồ sơ','Họ và tên','Năm sinh','Địa chỉ','Nghề nghiệp','Ghi chú']} ]}}));
  await page.locator('#ocrHeaderFile').setInputFiles({name:'cot.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:Buffer.from('fake')});
  await page.locator('#ocrScanHeaders').click();
  await expect(page.locator('.ocrHeaderSelected')).toHaveCount(6);
  await page.locator('#ocrCreateFromHeaders').click();
  await expect(page.locator('.ocrFieldRow')).toHaveCount(6);
  await expect(page.locator('.ocrExtract:checked')).toHaveCount(0);
  for(const index of [1,2,3,4])await page.locator('.ocrExtract').nth(index).check();
  await page.locator('#ocrSaveTemplate').click();
  await expect.poll(()=>saved?.fields?.length).toBe(6);
  expect(saved.fields.map((f:any)=>f.column)).toEqual(['Mã hồ sơ','Họ và tên','Năm sinh','Địa chỉ','Nghề nghiệp','Ghi chú']);
  expect(saved.fields.filter((f:any)=>f.extract).map((f:any)=>f.column)).toEqual(['Họ và tên','Năm sinh','Địa chỉ','Nghề nghiệp']);
});

test('CSV thật: chọn Tổ, lưu trường bổ sung và đọc lại nhân khẩu', async ({page},testInfo) => {
  await enterApp(page);
  const mobile=testInfo.project.name.startsWith('mobile');
  await (mobile?page.locator('[data-mobile-page="import"]'):page.locator('[data-page="import"]')).click();
  const name='Nguyễn CSV '+testInfo.project.name;
  await page.locator('#fileIn').setInputFiles({name:'dan-cu.csv',mimeType:'text/csv',
    buffer:Buffer.from('\uFEFFHọ và tên,Mã hộ,Năm sinh\n'+name+',CHUA-CO-'+testInfo.project.name+',1988\n')});
  await page.locator('#previewFile').click();
  await expect(page.locator('#importDefaultTeam')).toBeVisible();
  await page.locator('#importDefaultTeam').selectOption('3');
  page.on('dialog',dialog=>dialog.accept());
  await page.locator('#commitImport').click();
  await expect(page.getByRole('heading',{name:'Kết quả nhập'})).toBeVisible();
  await expect(page.locator('#importResult')).toContainText('chưa gắn hộ');
  await (mobile?page.locator('[data-mobile-page="residents"]'):page.locator('[data-page="residents"]')).click();
  await expect(page.getByText(name,{exact:true})).toBeVisible();
});
