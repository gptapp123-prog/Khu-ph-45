@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul 2>&1
where keytool >nul 2>&1
if errorlevel 1 (echo Can JDK keytool tren may quan tri phat hanh.& exit /b 2)
set "KEYDIR=%LOCALAPPDATA%\QuanLyKhuPho\ReleaseKeys"
if not exist "%KEYDIR%" mkdir "%KEYDIR%"
if errorlevel 1 exit /b 3
set "KEYFILE=%KEYDIR%\quanlykp-android-release.jks"
if exist "%KEYFILE%" (
  echo Khoa da ton tai: %KEYFILE%
  echo KHONG tao lai. Dung cung khoa nay cho moi ban cap nhat.
  keytool -list -v -keystore "%KEYFILE%" -alias qlkp-release
  if errorlevel 1 exit /b 5
  exit /b 0
)
echo Tao khoa phat hanh Android MOT LAN. Hay tu dat mat khau va luu ngoai GitHub.
keytool -genkeypair -v -keystore "%KEYFILE%" -alias qlkp-release -keyalg RSA -keysize 3072 -validity 10000 -dname "CN=Quan Ly Khu Pho, OU=Release, O=Quan Ly Khu Pho, C=VN"
if errorlevel 1 (echo Tao khoa that bai; kiem tra file dang do truoc khi thu lai.& exit /b 4)
echo Khoa: %KEYFILE%
echo Luu 2 ban sao ma hoa o noi an toan va ghi lai SHA-256 certificate fingerprint.
keytool -list -v -keystore "%KEYFILE%" -alias qlkp-release
if errorlevel 1 exit /b 5
exit /b 0
