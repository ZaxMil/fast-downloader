<#
.SYNOPSIS
    FastDownloader Pro 1-Liner Remote Installer.
    Usage in PowerShell:
    irm https://raw.githubusercontent.com/ZaxMil/fast-downloader/main/scripts/install.ps1 | iex
#>

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

Write-Host ""
Write-Host " ====================================================== " -ForegroundColor Cyan
Write-Host "    ⚡ FastDownloader Pro - 1-Liner Quick Installer      " -ForegroundColor Cyan
Write-Host " ====================================================== " -ForegroundColor Cyan
Write-Host ""

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\FastDownloader"
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# 1. Download latest archive from GitHub
Write-Host "[1/4] Downloading latest FastDownloader release..." -ForegroundColor Yellow
$ZipPath = Join-Path $env:TEMP "fast-downloader-latest.zip"
$ZipUrl = "https://github.com/ZaxMil/fast-downloader/archive/refs/heads/main.zip"
Invoke-WebRequest -Uri $ZipUrl -OutFile $ZipPath -UseBasicParsing

# 2. Extract into local application directory
Write-Host "[2/4] Extracting application files..." -ForegroundColor Yellow
$TempExtract = Join-Path $env:TEMP "fast-downloader-extract"
if (Test-Path $TempExtract) { Remove-Item $TempExtract -Recurse -Force }
Expand-Archive -Path $ZipPath -DestinationPath $TempExtract -Force

$SourceFolder = Join-Path $TempExtract "fast-downloader-main"
Get-ChildItem -Path $SourceFolder | Copy-Item -Destination $InstallDir -Recurse -Force

Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
Remove-Item $TempExtract -Recurse -Force -ErrorAction SilentlyContinue

# 3. Setup turbo engines
Write-Host "[3/4] Setting up high-speed download engines (aria2, yt-dlp, ffmpeg)..." -ForegroundColor Yellow
$SetupBinScript = Join-Path $InstallDir "scripts\setup-binaries.ps1"
if (Test-Path $SetupBinScript) {
    & $SetupBinScript
}

# 4. Compile binary if needed
$ExePath = Join-Path $InstallDir "FastDownloader.exe"
if (-not (Test-Path $ExePath)) {
    Write-Host "[4/4] Compiling native FastDownloader executable..." -ForegroundColor Yellow
    $CscPath = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $CscPath)) { $CscPath = "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    
    $SrcFile = Join-Path $InstallDir "src\Program.cs"
    $IcoFile = Join-Path $InstallDir "app.ico"
    
    & $CscPath /target:winexe /out:"$ExePath" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /win32icon:"$IcoFile" "$SrcFile"
}

# 5. Create Desktop & Start Menu Shortcuts
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

Write-Host ""
Write-Host "====================================================== " -ForegroundColor Green
Write-Host " 🎉 FastDownloader Pro installed successfully!        " -ForegroundColor Green
Write-Host "    Installed to: $InstallDir                          " -ForegroundColor Gray
Write-Host "    Desktop and Start Menu shortcuts created.          " -ForegroundColor Gray
Write-Host "====================================================== " -ForegroundColor Green
Write-Host ""

Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir
