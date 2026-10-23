@echo off
setlocal
title FastDownloader Pro - 1-Click Extension Setup
color 0b

echo  ======================================================
echo     FastDownloader Pro - 1-Click Extension Setup
echo  ======================================================
echo.

set "APP_DIR=%~dp0"
set "EXT_DIR=%~dp0extension"
set "JSON_HOST=%~dp0com.fastdownloader.app.json"

echo [*] Registering Native Messaging Host in Windows Registry...
reg add "HKCU\Software\Google\Chrome\NativeMessagingHosts\com.fastdownloader.app" /ve /t REG_SZ /d "%JSON_HOST%" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.fastdownloader.app" /ve /t REG_SZ /d "%JSON_HOST%" /f >nul 2>&1
reg add "HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.fastdownloader.app" /ve /t REG_SZ /d "%JSON_HOST%" /f >nul 2>&1

echo [OK] Native Messaging Host registered for Edge, Chrome, and Brave!
echo.
echo [*] Launching Browser with FastDownloader Extension pre-loaded...

set "LAUNCHED="
if exist "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" (
    start "" "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe" --load-extension="%EXT_DIR%" "edge://extensions"
    set "LAUNCHED=Microsoft Edge"
) else if exist "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" (
    start "" "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe" --load-extension="%EXT_DIR%" "edge://extensions"
    set "LAUNCHED=Microsoft Edge"
) else if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" (
    start "" "%ProgramFiles%\Google\Chrome\Application\chrome.exe" --load-extension="%EXT_DIR%" "chrome://extensions"
    set "LAUNCHED=Google Chrome"
) else if exist "%LocalAppData%\Google\Chrome\Application\chrome.exe" (
    start "" "%LocalAppData%\Google\Chrome\Application\chrome.exe" --load-extension="%EXT_DIR%" "chrome://extensions"
    set "LAUNCHED=Google Chrome"
)

if defined LAUNCHED (
    echo [OK] Started %LAUNCHED% with FastDownloader extension loaded!
) else (
    echo [!] Browser path not auto-detected. Open edge://extensions or chrome://extensions and click 'Load unpacked'.
)

echo.
echo  ======================================================
echo     Extension Setup Complete!
echo  ======================================================
ping 127.0.0.1 -n 3 >nul
exit /b 0
