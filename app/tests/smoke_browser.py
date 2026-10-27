"""Optional real-browser smoke check, no network/cloud/data fixtures.
Run: PYTHONPATH=pc python tests/smoke_browser.py
Requires local Chromium and playwright package.
"""
import socket,tempfile,threading,time
from pathlib import Path
import uvicorn
from playwright.sync_api import sync_playwright
from server.app import app,configure_vault

with tempfile.TemporaryDirectory() as d:
    configure_vault(str(Path(d)/'smoke.vault'),'PassphraseForSmoke_2026!')
    sock=socket.socket();sock.bind(('127.0.0.1',0));port=sock.getsockname()[1];sock.close()
    conf=uvicorn.Config(app,host='127.0.0.1',port=port,log_level='error',access_log=False)
    server=uvicorn.Server(conf)
    thread=threading.Thread(target=server.run,daemon=True);thread.start()
    for _ in range(50):
        if server.started:break
        time.sleep(.1)
    assert server.started,'Không khởi động được máy chủ'
    with sync_playwright() as pl:
        browser=pl.chromium.launch(headless=True,executable_path='/usr/bin/chromium',args=['--no-sandbox'])
        page=browser.new_page(viewport={'width':1280,'height':800},locale='vi-VN')
        errors=[]
        page.on('pageerror',lambda e: errors.append(str(e)))
        page.goto(f'http://127.0.0.1:{port}',wait_until='networkidle')
        page.locator('#authTitle').get_by_text('Thiết lập lần đầu').wait_for()
        page.locator('#username').fill('quantri')
        page.locator('#password').fill('KhoiTaoGiaoDien_2026!')
        page.locator('#setupName').fill('Nguyễn Quản Trị')
        page.locator('#loginButton').click()
        page.locator('#authTitle').get_by_text('Đăng nhập hệ thống').wait_for()
        page.locator('#loginButton').click()
        page.locator('#pageTitle').get_by_text('Tổng quan').wait_for()
        page.locator('[data-page="accounts"]').click()
        page.locator('#pageTitle').get_by_text('Tài khoản & phân quyền').wait_for()
        page.locator('#addAccount').click()
        page.locator('input[name="full_name"]').fill('Đinh Thị Ánh')
        page.locator('input[name="username"]').fill('dinh.anh')
        page.locator('input[name="password"]').fill('DinhThiAnh_2026!')
        page.locator('input[name="team_id"]').fill('3')
        page.locator('input[name="phone"]').fill('0912345678')
        page.locator('#modalForm button[type="submit"]').click()
        page.get_by_text('Đinh Thị Ánh').first.wait_for()
        page.locator('#accountSearch').fill('dinh.anh')
        row=page.locator('tr').filter(has_text='dinh.anh')
        assert row.is_visible()
        assert row.get_by_text('Chưa đổi mật khẩu').is_visible()
        page.locator('#changePasswordBtn').click()
        page.locator('input[name="old_password"]').fill('KhoiTaoGiaoDien_2026!')
        page.locator('input[name="new_password"]').fill('QuaMatKhauMoi_2026!')
        page.locator('#modalForm button[type="submit"]').click()
        page.locator('#authTitle').get_by_text('Đăng nhập hệ thống').wait_for()
        assert not errors,errors
        print('BROWSER_SMOKE_OK: setup/login/create_account/filter/change_password/logout; console errors=0')
        browser.close()
    server.should_exit=True
    thread.join(timeout=5)
