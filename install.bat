@echo off
setlocal enabledelayedexpansion
title FastDownloader Pro - 1-Click Installer
color 0b

echo  ======================================================
echo     ⚡  FastDownloader Pro - 1-Click Installer  ⚡
echo  ======================================================
echo.

:: 1. Compile FastDownloader.exe if not present
if not exist "%~dp0FastDownloader.exe" (
    echo [*] Compiling FastDownloader Pro binary...
    call "%~dp0build.bat"
    if !errorlevel! neq 0 (
        echo [!] Compilation failed. Please ensure .NET Framework 4.0 or higher is installed.
        pause
        exit /b 1
    )
) else (
    echo [OK] FastDownloader binary ready.
)

:: 2. Check and fetch helper engines (aria2c, yt-dlp, ffmpeg)
if not exist "%~dp0bin\aria2c.exe" (
    echo [*] Downloading high-velocity turbo engines (aria2, yt-dlp, ffmpeg)...
    powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0scripts\setup-binaries.ps1"
) else (
    echo [OK] Turbo engines already present in bin/
)

:: 3. Create Desktop & Start Menu Shortcuts
echo [*] Creating Desktop and Start Menu shortcuts...
powershell -ExecutionPolicy Bypass -NoProfile -Command ^
    "$ws = New-Object -ComObject WScript.Shell; " ^
    "$desk = [Environment]::GetFolderPath('Desktop'); " ^
    "$s1 = $ws.CreateShortcut(\"$desk\FastDownloader Pro.lnk\"); " ^
    "$s1.TargetPath = '%~dp0FastDownloader.exe'; " ^
    "$s1.WorkingDirectory = '%~dp0'; " ^
    "$s1.IconLocation = '%~dp0app.ico,0'; " ^
    "$s1.Description = 'FastDownloader Pro - Turbo Download Manager'; " ^
    "$s1.Save(); " ^
    "$start = [Environment]::GetFolderPath('Programs'); " ^
    "$s2 = $ws.CreateShortcut(\"$start\FastDownloader Pro.lnk\"); " ^
    "$s2.TargetPath = '%~dp0FastDownloader.exe'; " ^
    "$s2.WorkingDirectory = '%~dp0'; " ^
    "$s2.IconLocation = '%~dp0app.ico,0'; " ^
    "$s2.Description = 'FastDownloader Pro - Turbo Download Manager'; " ^
    "$s2.Save();"

echo [OK] Desktop and Start Menu shortcuts created!
echo.
echo  ======================================================
echo     🎉 Installation Complete!
echo     Launching FastDownloader Pro...
echo  ======================================================
echo.

start "" "%~dp0FastDownloader.exe"
timeout /t 3 >nul
exit /b 0
