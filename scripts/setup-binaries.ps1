<#
.SYNOPSIS
    Downloads official upstream helper engines (aria2c, yt-dlp, ffmpeg) into the bin/ directory.
.DESCRIPTION
    FastDownloader requires external CLI engines for parallel chunk downloading and stream muxing.
    To ensure full open-source licensing compliance and avoid bloated repository sizes, this script
    downloads authentic prebuilt releases directly from their respective official upstream repositories.
#>

$ErrorActionPreference = "Stop"
$BinDir = Join-Path $PSScriptRoot "..\bin"

if (-not (Test-Path $BinDir)) {
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " FastDownloader Engine Setup" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Download yt-dlp
$YtDlpDest = Join-Path $BinDir "yt-dlp.exe"
if (-not (Test-Path $YtDlpDest)) {
    Write-Host "[1/3] Downloading yt-dlp from official release..." -ForegroundColor Yellow
    $YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
    Invoke-WebRequest -Uri $YtDlpUrl -OutFile $YtDlpDest -UseBasicParsing
    Write-Host "  -> yt-dlp installed successfully." -ForegroundColor Green
} else {
    Write-Host "[1/3] yt-dlp.exe already present." -ForegroundColor Gray
}

# 2. Download aria2c (Windows 64-bit official release)
$Aria2Dest = Join-Path $BinDir "aria2c.exe"
if (-not (Test-Path $Aria2Dest)) {
    Write-Host "[2/3] Downloading aria2 from official release..." -ForegroundColor Yellow
    $AriaZip = Join-Path $env:TEMP "aria2.zip"
    $AriaUrl = "https://github.com/aria2/aria2/releases/download/release-1.37.0/aria2-1.37.0-win-64bit-build1.zip"
    Invoke-WebRequest -Uri $AriaUrl -OutFile $AriaZip -UseBasicParsing
    
    $TempExtract = Join-Path $env:TEMP "aria2_extract"
    if (Test-Path $TempExtract) { Remove-Item $TempExtract -Recurse -Force }
    Expand-Archive -Path $AriaZip -DestinationPath $TempExtract -Force
    
    $AriaExe = Get-ChildItem -Path $TempExtract -Filter "aria2c.exe" -Recurse | Select-Object -First 1
    if ($AriaExe) {
        Copy-Item -Path $AriaExe.FullName -Destination $Aria2Dest -Force
        Write-Host "  -> aria2c installed successfully." -ForegroundColor Green
    }
    Remove-Item $AriaZip -Force -ErrorAction SilentlyContinue
    Remove-Item $TempExtract -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "[2/3] aria2c.exe already present." -ForegroundColor Gray
}

# 3. Download FFmpeg essentials build
$FfmpegDest = Join-Path $BinDir "ffmpeg.exe"
if (-not (Test-Path $FfmpegDest)) {
    Write-Host "[3/3] Downloading ffmpeg essentials..." -ForegroundColor Yellow
    $FfmpegZip = Join-Path $env:TEMP "ffmpeg.zip"
    $FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
    Invoke-WebRequest -Uri $FfmpegUrl -OutFile $FfmpegZip -UseBasicParsing
    
    $TempExtractFfmpeg = Join-Path $env:TEMP "ffmpeg_extract"
    if (Test-Path $TempExtractFfmpeg) { Remove-Item $TempExtractFfmpeg -Recurse -Force }
    Expand-Archive -Path $FfmpegZip -DestinationPath $TempExtractFfmpeg -Force
    
    $FfmpegExe = Get-ChildItem -Path $TempExtractFfmpeg -Filter "ffmpeg.exe" -Recurse | Select-Object -First 1
    if ($FfmpegExe) {
        Copy-Item -Path $FfmpegExe.FullName -Destination $FfmpegDest -Force
        Write-Host "  -> ffmpeg installed successfully." -ForegroundColor Green
    }
    Remove-Item $FfmpegZip -Force -ErrorAction SilentlyContinue
    Remove-Item $TempExtractFfmpeg -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "[3/3] ffmpeg.exe already present." -ForegroundColor Gray
}

Write-Host "`nAll engines ready in bin/ directory!" -ForegroundColor Green
