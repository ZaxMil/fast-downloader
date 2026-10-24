# ==============================================================================
# FastDownloader Pro - One-File Setup Wizard Packager
# Builds a self-contained, single-file installer (FastDownloader-Setup.exe)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$RootDir = Split-Path -Parent $PSScriptRoot
$SetupExe = Join-Path $RootDir "FastDownloader-Setup.exe"
$TempPayloadDir = Join-Path $env:TEMP "FastDownloader_Installer_Payload"
$PayloadZip = Join-Path $env:TEMP "FastDownloader_payload.zip"

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "    FastDownloader Pro - Setup Wizard Packager        " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Compile Main Application if needed
$MainExe = Join-Path $RootDir "FastDownloader.exe"
if (-not (Test-Path $MainExe)) {
    Write-Host "[1/4] Compiling FastDownloader.exe..." -ForegroundColor Yellow
    & (Join-Path $RootDir "build.bat")
} else {
    Write-Host "[1/4] [OK] FastDownloader.exe is ready." -ForegroundColor Green
}

# 2. Stage Payload Files
Write-Host "[2/4] Staging payload files for embedding..." -ForegroundColor Yellow
if (Test-Path $TempPayloadDir) { Remove-Item $TempPayloadDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempPayloadDir | Out-Null

$FilesToCopy = @(
    "FastDownloader.exe",
    "app.ico",
    "com.fastdownloader.app.json",
    "LICENSE",
    "DISCLAIMER.md",
    "PRIVACY.md",
    "README.md"
)

foreach ($file in $FilesToCopy) {
    $src = Join-Path $RootDir $file
    if (Test-Path $src) {
        Copy-Item $src -Destination $TempPayloadDir -Force
    }
}

# Copy UI and Extension directories
Copy-Item (Join-Path $RootDir "ui") -Destination $TempPayloadDir -Recurse -Force
Copy-Item (Join-Path $RootDir "extension") -Destination $TempPayloadDir -Recurse -Force

# Copy core bin engines (aria2c and yt-dlp)
$BinDir = Join-Path $RootDir "bin"
if (Test-Path $BinDir) {
    $BinTarget = Join-Path $TempPayloadDir "bin"
    New-Item -ItemType Directory -Path $BinTarget -Force | Out-Null
    
    $Aria2 = Join-Path $BinDir "aria2c.exe"
    if (Test-Path $Aria2) { Copy-Item $Aria2 -Destination $BinTarget -Force }
    
    $YtDlp = Join-Path $BinDir "yt-dlp.exe"
    if (Test-Path $YtDlp) { Copy-Item $YtDlp -Destination $BinTarget -Force }
}

# 3. Create Payload Zip
Write-Host "[3/4] Compressing payload into bundle..." -ForegroundColor Yellow
if (Test-Path $PayloadZip) { Remove-Item $PayloadZip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($TempPayloadDir, $PayloadZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$PayloadSizeMb = (Get-Item $PayloadZip).Length / 1MB
Write-Host ("      Payload compressed size: {0:N2} MB" -f $PayloadSizeMb) -ForegroundColor Gray

# 4. Compile Setup Wizard Executable with Embedded Payload
Write-Host "[4/4] Compiling self-contained FastDownloader-Setup.exe..." -ForegroundColor Yellow

$CscPath = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $CscPath)) {
    $CscPath = "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

$WizardSrc = Join-Path $RootDir "src\Installer\WpfSetupWizard.cs"
$IconFile = Join-Path $RootDir "installer.ico"
if (-not (Test-Path $IconFile)) { $IconFile = Join-Path $RootDir "app.ico" }

if (Test-Path $SetupExe) {
    Remove-Item $SetupExe -Force -ErrorAction SilentlyContinue
}

$WpfDir = Join-Path $env:SystemRoot "Microsoft.NET\Framework64\v4.0.30319\WPF"
if (-not (Test-Path $WpfDir)) {
    $WpfDir = Join-Path $env:SystemRoot "Microsoft.NET\Framework\v4.0.30319\WPF"
}

$CscArgs = @(
    "/target:winexe",
    "/out:`"$SetupExe`"",
    "/win32icon:`"$IconFile`"",
    "/resource:`"$PayloadZip`",payload.zip",
    "/lib:`"$WpfDir`"",
    "/r:PresentationFramework.dll",
    "/r:PresentationCore.dll",
    "/r:WindowsBase.dll",
    "/r:System.Xaml.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.IO.Compression.dll",
    "/r:System.IO.Compression.FileSystem.dll",
    "/optimize+",
    "`"$WizardSrc`""
)

$cmd = "$CscPath " + ($CscArgs -join " ")
cmd.exe /c $cmd | Out-Null

# Cleanup temporary files
Remove-Item $TempPayloadDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $PayloadZip -Force -ErrorAction SilentlyContinue

if (Test-Path $SetupExe) {
    $SetupSizeMb = (Get-Item $SetupExe).Length / 1MB
    Write-Host ""
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host (" [SUCCESS] Built single-file installer: {0:N2} MB" -f $SetupSizeMb) -ForegroundColor Green
    Write-Host " Output: $SetupExe" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[ERROR] Failed to compile FastDownloader-Setup.exe" -ForegroundColor Red
    exit 1
}
