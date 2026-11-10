@echo off
setlocal
cd /d "%~dp0"
title QLKP PILOT V0.20.6 - XAY DUNG SETUP
if /I not "%PROCESSOR_ARCHITECTURE%"=="AMD64" if /I not "%PROCESSOR_ARCHITEW6432%"=="AMD64" (
 echo Can he dieu hanh Windows x64 de build ban nay.
 pause
 exit /b 1
)
echo QLKP V0.20 PILOT - Tu dong tai cong cu chinh thuc neu thieu, build, self-test, dong goi.
echo Luu y: se cai .NET SDK / VS Build Tools / Inno Setup bang winget khi can.
echo KHONG duoc nhap du lieu ca nhan that vao ban pilot.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-on-windows.ps1" -BootstrapPrereqs -Installer -SmokeTest
if errorlevel 1 (
 echo.
 echo BUILD THAT BAI: xem build-logs va tai lieu huong dan. Khong co bo cai hop le.
 pause
 exit /b 1
)
echo.
echo BUILD DA XONG. Vui long mo thu muc dist de lay SETUP.EXE.
explorer "%~dp0dist"
pause
