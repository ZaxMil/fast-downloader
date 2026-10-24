@echo off
setlocal
title FastDownloader Pro - Build Installer
color 0b

echo  ======================================================
echo     Building FastDownloader Pro Setup Executable
echo  ======================================================
echo.

powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0scripts\package-installer.ps1"
if errorlevel 1 (
    echo.
    echo [!] Build failed!
    pause
    exit /b 1
)

echo.
echo Setup builder finished.
exit /b 0
