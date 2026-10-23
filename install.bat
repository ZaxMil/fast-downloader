@echo off
setlocal
title FastDownloader Pro - 1-Click Installer
color 0b

echo  ======================================================
echo     FastDownloader Pro - 1-Click Installer
echo  ======================================================
echo.

REM 1. Compile FastDownloader.exe if not present
if not exist "%~dp0FastDownloader.exe" (
    echo [*] Compiling FastDownloader Pro binary...
    call "%~dp0build.bat"
    if errorlevel 1 (
        echo [!] Compilation failed. Please ensure .NET Framework 4.0 or higher is installed.
        pause
        exit /b 1
    )
) else (
    echo [OK] FastDownloader binary ready.
)

REM 2. Check and fetch helper engines with turbo parallel acceleration
if not exist "%~dp0bin\aria2c.exe" (
    echo [*] Accelerating turbo engine setup with 16 parallel connections...
    powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0scripts\setup-binaries.ps1"
) else (
    echo [OK] Turbo engines already present in bin/
)

REM 3. Register Native Messaging Host for Browser Extension
echo [*] Registering Browser Extension integration...
reg add "HKCU\Software\Google\Chrome\NativeMessagingHosts\com.fastdownloader.app" /ve /t REG_SZ /d "%~dp0com.fastdownloader.app.json" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.fastdownloader.app" /ve /t REG_SZ /d "%~dp0com.fastdownloader.app.json" /f >nul 2>&1
reg add "HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.fastdownloader.app" /ve /t REG_SZ /d "%~dp0com.fastdownloader.app.json" /f >nul 2>&1

REM 4. Create Desktop and Start Menu Shortcuts
echo [*] Creating Desktop and Start Menu shortcuts...
powershell -ExecutionPolicy Bypass -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $d = [Environment]::GetFolderPath('Desktop'); $s1 = $ws.CreateShortcut(\"$d\FastDownloader Pro.lnk\"); $s1.TargetPath = '%~dp0FastDownloader.exe'; $s1.WorkingDirectory = '%~dp0'; $s1.IconLocation = '%~dp0app.ico,0'; $s1.Description = 'FastDownloader Pro'; $s1.Save(); $p = [Environment]::GetFolderPath('Programs'); $s2 = $ws.CreateShortcut(\"$p\FastDownloader Pro.lnk\"); $s2.TargetPath = '%~dp0FastDownloader.exe'; $s2.WorkingDirectory = '%~dp0'; $s2.IconLocation = '%~dp0app.ico,0'; $s2.Description = 'FastDownloader Pro'; $s2.Save();"

echo [OK] Shortcuts and browser integration registered!
echo.
echo  ======================================================
echo     Installation Complete!
echo     Launching FastDownloader Pro...
echo  ======================================================
echo.

start "" "%~dp0FastDownloader.exe"
ping 127.0.0.1 -n 2 >nul
exit /b 0
