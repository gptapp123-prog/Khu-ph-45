@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul 2>&1
set "INSTALL=%LOCALAPPDATA%\QuanLyKhuPho\Program"
if not "%~1"=="" set "INSTALL=%~1"
echo ===========================================================
echo     QUAN LY KHU PHO 7.5 - CAI DAT PC THU
echo ===========================================================
echo Chuong trinh: %INSTALL%
echo Du lieu: %LOCALAPPDATA%\QuanLyKhuPho\Data
echo.
if not exist "%~dp0pc\win_compat.py" (
  echo LOI: Bo nguon khong day du.
  exit /b 2
)
set "PY=py -3"
where py >nul 2>&1
if errorlevel 1 set "PY=python"
%PY% "%~dp0pc\win_compat.py"
if errorlevel 1 (
  echo LOI: Kiem tra Python/Windows khong dat.
  echo Windows 7/8: can Python 3.8. Windows 8.1/10/11: can Python 3.11-3.15.
  exit /b 3
)
set "DEP_FILE="
for /f "usebackq delims=" %%i in (`%PY% "%~dp0pc\win_compat.py" --requirements`) do set "DEP_FILE=%%i"
if "%DEP_FILE%"=="" (echo Khong xac dinh duoc thu vien.& exit /b 4)
if not exist "%INSTALL%" mkdir "%INSTALL%"
if errorlevel 1 (echo Khong the tao thu muc cai dat.& exit /b 5)
if not exist "%INSTALL%\.venv\Scripts\python.exe" %PY% -m venv "%INSTALL%\.venv"
if errorlevel 1 (echo Khong tao duoc venv.& exit /b 7)
"%INSTALL%\.venv\Scripts\python.exe" "%~dp0pc\win_compat.py"
if errorlevel 1 (echo Moi truong cu khong tuong thich, can sao luu va tai cai thu vien.& exit /b 8)
echo Dang cai cac thu vien: %DEP_FILE%
if exist "%~dp0wheelhouse\" (
  echo Dang cai tu bo thu vien offline tren PC.
  "%INSTALL%\.venv\Scripts\python.exe" -m pip install --no-index --find-links "%~dp0wheelhouse" -r "%~dp0%DEP_FILE%"
) else (
  echo Khong co wheelhouse; pip can ket noi de cai thu vien.
  "%INSTALL%\.venv\Scripts\python.exe" -m pip install -r "%~dp0%DEP_FILE%"
)
if errorlevel 1 (echo Cai thu vien loi; chua chep ma nguon moi. Khong co thay doi nao tren kho du lieu PC.& exit /b 9)
"%INSTALL%\.venv\Scripts\python.exe" -c "import fastapi,uvicorn,cryptography,argon2,openpyxl,pypdf,pytesseract,PIL,zxingcpp"
if errorlevel 1 (echo Thu vien khong day du; chua chep ma nguon moi.& exit /b 10)
if "%DEP_FILE%"=="requirements.txt" (
  "%INSTALL%\.venv\Scripts\python.exe" -c "import pypdfium2"
  if errorlevel 1 (echo Thu vien PDF khong day du; chua chep ma nguon moi.& exit /b 10)
)
if exist "%INSTALL%\pc.next" (echo Thu muc pc.next dang do dang; can kiem tra truoc khi thu lai.& exit /b 11)
if exist "%INSTALL%\pc.previous" (echo Da co ban pc.previous; can sao luu va di chuyen truoc khi nang cap tiep.& exit /b 11)
mkdir "%INSTALL%\pc.next"
if errorlevel 1 exit /b 11
robocopy "%~dp0pc" "%INSTALL%\pc.next" /E /R:1 /W:1 /XD __pycache__ .pytest_cache /XF *.pyc >nul
if errorlevel 8 (echo Loi sao chep ma nguon.& exit /b 12)
if exist "%INSTALL%\pc" (
  move "%INSTALL%\pc" "%INSTALL%\pc.previous" >nul
  if errorlevel 1 (echo Khong the dong ban cu; hay tat ung dung roi thu lai.& exit /b 13)
)
move "%INSTALL%\pc.next" "%INSTALL%\pc" >nul
if errorlevel 1 (
  if exist "%INSTALL%\pc.previous" move "%INSTALL%\pc.previous" "%INSTALL%\pc" >nul
  echo Loi kich hoat ban moi; kiem tra pc.previous va pc.next truoc khi thu lai.
  exit /b 14
)
copy /Y "%~dp0requirements.txt" "%INSTALL%\requirements.txt" >nul
copy /Y "%~dp0requirements-win7-legacy.txt" "%INSTALL%\requirements-win7-legacy.txt" >nul
(
 echo @echo off
 echo cd /d "%%~dp0pc"
 echo "%%~dp0.venv\Scripts\python.exe" "%%~dp0pc\launcher.py" %%*
 echo pause
)>"%INSTALL%\Khoi_dong_PC.bat"
echo.
echo DA CAI MA NGUON VA THU VIEN. Chua phai bo cai EXE/MSI.
where tesseract >nul 2>&1
if errorlevel 1 echo CANH BAO: Tesseract OCR chua co trong PATH. Can cai rieng de doc anh, scan va PDF anh.
echo Khoi dong: "%INSTALL%\Khoi_dong_PC.bat"
echo Du lieu van nam TREN PC. Can thu nghiem truoc khi nhap du lieu that.
exit /b 0
