# FastDownloader Pro - Release Packager
# Creates FastDownloader-AllInOne.zip (includes pre-built app, ui, extension, and turbo engines)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$RootDir = Split-Path -Parent $PSScriptRoot
$ZipPath = Join-Path $RootDir "FastDownloader-AllInOne.zip"
$LiteZipPath = Join-Path $RootDir "FastDownloader-Lite.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem

# 1. Ensure FastDownloader.exe is compiled
$ExePath = Join-Path $RootDir "FastDownloader.exe"
if (-not (Test-Path $ExePath)) {
    Write-Host "[*] Compiling FastDownloader.exe..." -ForegroundColor Yellow
    & (Join-Path $RootDir "build.bat")
}

# 2. Package All-In-One (Everything included: prebuilt binaries, ui, extension)
Write-Host "[*] Packaging All-In-One release..." -ForegroundColor Cyan
$TempDir = Join-Path $env:TEMP "FastDownloader_Release_Package"
if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

$FilesToCopy = @(
    "FastDownloader.exe",
    "app.ico",
    "com.fastdownloader.app.json",
    "install.bat",
    "install-extension.bat",
    "README.md",
    "LICENSE",
    "DISCLAIMER.md"
)

foreach ($f in $FilesToCopy) {
    $src = Join-Path $RootDir $f
    if (Test-Path $src) { Copy-Item $src -Destination $TempDir }
}

Copy-Item (Join-Path $RootDir "ui") -Destination $TempDir -Recurse
Copy-Item (Join-Path $RootDir "extension") -Destination $TempDir -Recurse

# Copy bin folder if present (aria2c, yt-dlp, ffmpeg)
$BinDir = Join-Path $RootDir "bin"
if (Test-Path $BinDir) {
    Copy-Item $BinDir -Destination $TempDir -Recurse
}

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($TempDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$SizeAio = (Get-Item $ZipPath).Length / 1MB
Write-Host ("  -> Created FastDownloader-AllInOne.zip ({0:N1} MB)" -f $SizeAio) -ForegroundColor Green

# 3. Create Lite version (without 220MB ffmpeg for instant 20MB download)
Write-Host "[*] Packaging Lite release (20 MB - fetches ffmpeg on demand)..." -ForegroundColor Cyan
$BinTemp = Join-Path $TempDir "bin"
if (Test-Path (Join-Path $BinTemp "ffmpeg.exe")) {
    Remove-Item (Join-Path $BinTemp "ffmpeg.exe") -Force
}
if (Test-Path $LiteZipPath) { Remove-Item $LiteZipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($TempDir, $LiteZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$SizeLite = (Get-Item $LiteZipPath).Length / 1MB
Write-Host ("  -> Created FastDownloader-Lite.zip ({0:N1} MB)" -f $SizeLite) -ForegroundColor Green

Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "`nAll release packages created successfully!" -ForegroundColor Green
