import { chromium } from '@playwright/test';
import fs from 'node:fs/promises';

const baseURL = process.env.BASE_URL || 'https://khu-pho-45-phuong-binh-tan-iwhddk.v2.appdeploy.ai/';
const out = process.env.AUTH_STATE_PATH || 'playwright/.auth/user.json';

await fs.mkdir('playwright/.auth', { recursive: true });
const browser = await chromium.launch({ headless: false });
const context = await browser.newContext();
const page = await context.newPage();
await page.goto(baseURL, { waitUntil: 'domcontentloaded' });
await page.getByRole('button', { name: /Đăng nhập Google/i }).click();
console.log('Complete Google sign-in in the opened browser window.');
await page.getByRole('button', { name: /Mã tài khoản/i }).waitFor({ timeout: 180000 });
await context.storageState({ path: out, indexedDB: true });
console.log(`Saved authenticated browser state to ${out}`);
await browser.close();
