@echo off
setlocal

cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1"

if errorlevel 1 (
    echo.
    echo Startup failed. Check scratch\logs for details.
    pause
    exit /b %errorlevel%
)

exit /b 0
