<#
.SYNOPSIS
    FastDownloader Pro 1-Liner Remote Installer.
    Usage in PowerShell:
    irm https://raw.githubusercontent.com/ZaxMil/fast-downloader/main/scripts/install.ps1 | iex
#>

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host ""
Write-Host " ====================================================== " -ForegroundColor Cyan
Write-Host "    ⚡ FastDownloader Pro - Turbo Quick Installer       " -ForegroundColor Cyan
Write-Host " ====================================================== " -ForegroundColor Cyan
Write-Host ""

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\FastDownloader"
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$webClient = New-Object System.Net.WebClient

# 1. Download latest archive from GitHub
Write-Host "[1/4] 🚀 Fetching FastDownloader application package..." -ForegroundColor Yellow
$ZipPath = Join-Path $env:TEMP "fast-downloader-latest.zip"
$ZipUrl = "https://github.com/ZaxMil/fast-downloader/archive/refs/heads/main.zip"
$webClient.DownloadFile($ZipUrl, $ZipPath)

# 2. Extract instantly via .NET ZipFile
Write-Host "[2/4] ⚡ Extracting files..." -ForegroundColor Yellow
$TempExtract = Join-Path $env:TEMP "fast-downloader-extract"
if (Test-Path $TempExtract) { Remove-Item $TempExtract -Recurse -Force }

[System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $TempExtract)

$SourceFolder = Join-Path $TempExtract "fast-downloader-main"
Get-ChildItem -Path $SourceFolder | Copy-Item -Destination $InstallDir -Recurse -Force

Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
Remove-Item $TempExtract -Recurse -Force -ErrorAction SilentlyContinue

# 3. Compile binary if needed
$ExePath = Join-Path $InstallDir "FastDownloader.exe"
if (-not (Test-Path $ExePath)) {
    Write-Host "[3/4] ⚙️ Compiling native FastDownloader executable..." -ForegroundColor Yellow
    $CscPath = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $CscPath)) { $CscPath = "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    
    $SrcFile = Join-Path $InstallDir "src\Program.cs"
    $IcoFile = Join-Path $InstallDir "app.ico"
    
    & $CscPath /target:winexe /out:"$ExePath" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /win32icon:"$IcoFile" "$SrcFile" | Out-Null
} else {
    Write-Host "[3/4] [OK] FastDownloader executable ready." -ForegroundColor Gray
}

# 4. Setup turbo engines with 16x acceleration
Write-Host "[4/4] ⚡ Initializing 16-connection download engines..." -ForegroundColor Yellow
$SetupBinScript = Join-Path $InstallDir "scripts\setup-binaries.ps1"
if (Test-Path $SetupBinScript) {
    & $SetupBinScript
}

# 5. Auto-register Native Messaging Host for Chrome & Edge
try {
    $JsonHostPath = Join-Path $InstallDir "com.fastdownloader.app.json"
    if (Test-Path $JsonHostPath) {
        $regEdge = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.fastdownloader.app"
        $regChrome = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.fastdownloader.app"
        New-Item -Path $regEdge -Force | Out-Null
        Set-ItemProperty -Path $regEdge -Name "(Default)" -Value $JsonHostPath -Force | Out-Null
        New-Item -Path $regChrome -Force | Out-Null
        Set-ItemProperty -Path $regChrome -Name "(Default)" -Value $JsonHostPath -Force | Out-Null
    }
} catch { }

# 6. Create Desktop & Start Menu Shortcuts
$WshShell = New-Object -ComObject WScript.Shell
$DesktopPath = [Environment]::GetFolderPath('Desktop')
$Shortcut1 = $WshShell.CreateShortcut((Join-Path $DesktopPath "FastDownloader Pro.lnk"))
$Shortcut1.TargetPath = $ExePath
$Shortcut1.WorkingDirectory = $InstallDir
$Shortcut1.IconLocation = "$InstallDir\app.ico,0"
$Shortcut1.Description = "FastDownloader Pro - Turbo Download Manager"
$Shortcut1.Save()

$ProgramsPath = [Environment]::GetFolderPath('Programs')
$Shortcut2 = $WshShell.CreateShortcut((Join-Path $ProgramsPath "FastDownloader Pro.lnk"))
$Shortcut2.TargetPath = $ExePath
$Shortcut2.WorkingDirectory = $InstallDir
$Shortcut2.IconLocation = "$InstallDir\app.ico,0"
$Shortcut2.Description = "FastDownloader Pro - Turbo Download Manager"
$Shortcut2.Save()

$webClient.Dispose()

Write-Host ""
Write-Host "====================================================== " -ForegroundColor Green
Write-Host " 🎉 FastDownloader Pro installed successfully!        " -ForegroundColor Green
Write-Host "    Installed to: $InstallDir                          " -ForegroundColor Gray
Write-Host "    Desktop and Start Menu shortcuts created.          " -ForegroundColor Gray
Write-Host "====================================================== " -ForegroundColor Green
Write-Host ""

Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir
