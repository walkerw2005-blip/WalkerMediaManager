@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Installer.ps1"
set exitcode=%errorlevel%
if %exitcode%==2 (
  echo.
  echo Install Inno Setup 6 and run this file again to create the installer.
  pause
  exit /b 2
)
if not %exitcode%==0 (
  echo.
  echo Installer build failed. Review the message above.
  pause
  exit /b %exitcode%
)
pause
