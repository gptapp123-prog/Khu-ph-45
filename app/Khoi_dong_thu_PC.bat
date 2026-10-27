@echo off
setlocal
chcp 65001 >nul 2>&1
cd /d "%~dp0"
set "PY=py -3"
where py >nul 2>&1
if errorlevel 1 set "PY=python"
%PY% pc\win_compat.py
if errorlevel 1 (
  echo Khong the chay bo nguon nay tren moi truong hien tai.
  pause
  exit /b 3
)
%PY% pc\launcher.py
pause
