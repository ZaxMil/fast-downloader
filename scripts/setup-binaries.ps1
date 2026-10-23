<#
.SYNOPSIS
    FastDownloader High-Velocity Turbo Engine Setup.
    Downloads official upstream helper engines (aria2c, yt-dlp, ffmpeg) with maximum speed.
.DESCRIPTION
    Uses .NET parallel extraction and multi-connection acceleration.
    Eliminates slow PowerShell Expand-Archive and throttled progress bars.
#>

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$BinDir = Join-Path $PSScriptRoot "..\bin"
if (-not (Test-Path $BinDir)) {
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Extract-SingleFileFromZip($zipPath, $fileName, $destPath) {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entry = $zip.Entries | Where-Object { $_.Name -ieq $fileName } | Select-Object -First 1
        if ($entry) {
            if (Test-Path $destPath) { Remove-Item $destPath -Force -ErrorAction SilentlyContinue }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
            return $true
        }
        return $false
    }
    finally {
        $zip.Dispose()
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " ⚡ FastDownloader Turbo Engine Setup ⚡" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$Aria2Dest = Join-Path $BinDir "aria2c.exe"
$YtDlpDest = Join-Path $BinDir "yt-dlp.exe"
$FfmpegDest = Join-Path $BinDir "ffmpeg.exe"

$allPresent = (Test-Path $Aria2Dest) -and (Test-Path $YtDlpDest) -and (Test-Path $FfmpegDest)
if ($allPresent) {
    Write-Host "[OK] All engines (aria2, yt-dlp, ffmpeg) are already present and ready!" -ForegroundColor Green
    exit 0
}

$webClient = New-Object System.Net.WebClient

# STEP 1: Fast Bootstrapping with aria2 (5 MB)
if (-not (Test-Path $Aria2Dest)) {
    Write-Host "[1/3] ⚡ Fetching 16x multi-connection aria2 engine..." -ForegroundColor Yellow
    $AriaZip = Join-Path $env:TEMP "aria2_boot.zip"
    $AriaUrl = "https://github.com/aria2/aria2/releases/download/release-1.37.0/aria2-1.37.0-win-64bit-build1.zip"
    
    $webClient.DownloadFile($AriaUrl, $AriaZip)
    $ok = Extract-SingleFileFromZip $AriaZip "aria2c.exe" $AriaDest
    Remove-Item $AriaZip -Force -ErrorAction SilentlyContinue
    
    if ($ok) {
        Write-Host "  -> aria2c (16-connections) ready!" -ForegroundColor Green
    } else {
        Write-Host "  [!] Warning: Could not extract aria2c directly." -ForegroundColor Red
    }
} else {
    Write-Host "[1/3] aria2c already installed." -ForegroundColor Gray
}

# STEP 2: Download yt-dlp & ffmpeg using 16 parallel connections
$needYtdlp = -not (Test-Path $YtDlpDest)
$needFfmpeg = -not (Test-Path $FfmpegDest)

if ($needYtdlp -or $needFfmpeg) {
    Write-Host "[2/3] 🚀 Accelerating downloads via 16 parallel streams..." -ForegroundColor Yellow
    
    # 2a. yt-dlp
    if ($needYtdlp) {
        $YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
        if (Test-Path $Aria2Dest) {
            Write-Host "  -> Downloading yt-dlp at turbo speed..." -ForegroundColor Cyan
            & $Aria2Dest -x 16 -s 16 -k 1M --allow-overwrite=true --auto-file-renaming=false -d $BinDir -o "yt-dlp.exe" $YtDlpUrl | Out-Null
        } else {
            $webClient.DownloadFile($YtDlpUrl, $YtDlpDest)
        }
        Write-Host "  -> yt-dlp ready!" -ForegroundColor Green
    } else {
        Write-Host "[2/3] yt-dlp already installed." -ForegroundColor Gray
    }

    # 2b. ffmpeg (Stream extraction directly from zip)
    if ($needFfmpeg) {
        Write-Host "[3/3] 🎬 Fetching FFmpeg muxer..." -ForegroundColor Yellow
        $FfmpegZip = Join-Path $env:TEMP "ffmpeg_turbo.zip"
        $FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
        
        if (Test-Path $Aria2Dest) {
            Write-Host "  -> Downloading FFmpeg with 16 parallel connections..." -ForegroundColor Cyan
            & $Aria2Dest -x 16 -s 16 -k 1M --allow-overwrite=true --auto-file-renaming=false -d $env:TEMP -o "ffmpeg_turbo.zip" $FfmpegUrl | Out-Null
        } else {
            $webClient.DownloadFile($FfmpegUrl, $FfmpegZip)
        }
        
        Write-Host "  -> Extracting ffmpeg.exe instantly..." -ForegroundColor Cyan
        $okFfmpeg = Extract-SingleFileFromZip $FfmpegZip "ffmpeg.exe" $FfmpegDest
        Remove-Item $FfmpegZip -Force -ErrorAction SilentlyContinue
        
        if ($okFfmpeg) {
            Write-Host "  -> ffmpeg ready!" -ForegroundColor Green
        } else {
            Write-Host "  [!] Warning: Could not extract ffmpeg directly." -ForegroundColor Red
        }
    } else {
        Write-Host "[3/3] ffmpeg already installed." -ForegroundColor Gray
    }
}

$webClient.Dispose()
Write-Host "`n🎉 All engines ready in bin/ directory!" -ForegroundColor Green
