@echo off
setlocal
echo ==============================================
echo  FastDownloader Pro - Quick Build Script
echo ==============================================

set CSC_PATH=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    set CSC_PATH=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC_PATH%" (
    echo [ERROR] .NET Framework 4.0/4.8 csc.exe compiler not found!
    pause
    exit /b 1
)

echo [1/2] Compiling src\Program.cs...
"%CSC_PATH%" /target:winexe /out:"FastDownloader.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /win32icon:"app.ico" "src\Program.cs"

if %ERRORLEVEL% equ 0 (
    echo [2/2] Build successful! Created FastDownloader.exe
) else (
    echo [ERROR] Build failed with error code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

endlocal
