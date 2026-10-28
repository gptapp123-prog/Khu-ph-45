@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul 2>&1
cd /d "%~dp0"
set "PY=py -3"
where py >nul 2>&1
if errorlevel 1 set "PY=python"
%PY% pc\win_compat.py
if errorlevel 1 (echo Python/Windows khong phu hop de tao goi.& exit /b 3)
set "DEP_FILE="
for /f "usebackq delims=" %%i in (`%PY% pc\win_compat.py --requirements`) do set "DEP_FILE=%%i"
if "%DEP_FILE%"=="" exit /b 4
set "DEST=%~dp0dist\QuanLyKhuPho_PC_offline"
if exist "%DEST%" (echo Thu muc dist da ton tai; doi ten hoac di chuyen truoc khi tao lai.& exit /b 5)
mkdir "%DEST%\pc" "%DEST%\wheelhouse"
if errorlevel 1 exit /b 6
robocopy "%~dp0pc" "%DEST%\pc" /E /R:1 /W:1 /XD __pycache__ .pytest_cache /XF *.pyc >nul
if errorlevel 8 exit /b 7
copy /Y "Cai_dat_PC.bat" "%DEST%\" >nul
copy /Y "requirements.txt" "%DEST%\" >nul
copy /Y "requirements-win7-legacy.txt" "%DEST%\" >nul
%PY% -m pip download --only-binary=:all: -r "%DEP_FILE%" -d "%DEST%\wheelhouse"
if errorlevel 1 (echo Tai wheel that bai; goi dang tao khong day du. Khong phat hanh.& exit /b 8)
%PY% -m pip install --dry-run --ignore-installed --no-index --find-links "%DEST%\wheelhouse" -r "%DEP_FILE%"
if errorlevel 1 (echo Thieu thu vien offline; khong phat hanh.& exit /b 9)
echo DA TAO: %DEST%
echo Chi dung tren Windows/Python cung nen tang, kien truc voi may tao goi.
echo Tesseract OCR executable can cai rieng va co trong PATH.
echo Chay Cai_dat_PC.bat tren may kiem thu truoc khi phat hanh.
exit /b 0
