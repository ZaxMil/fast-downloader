$RootDir = Split-Path -Parent $PSScriptRoot
Set-Location $RootDir

Write-Host "1. Building FastDownloader.exe..." -ForegroundColor Cyan
& ".\build.bat"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build FastDownloader.exe" -ForegroundColor Red
    exit 1
}

Write-Host "2. Packaging fast-downloader-extension.zip..." -ForegroundColor Cyan
$ExtZip = Join-Path $RootDir "fast-downloader-extension.zip"
if (Test-Path $ExtZip) { Remove-Item $ExtZip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $RootDir "extension"), $ExtZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "3. Packaging fast-downloader-ui.zip..." -ForegroundColor Cyan
$UiZip = Join-Path $RootDir "fast-downloader-ui.zip"
if (Test-Path $UiZip) { Remove-Item $UiZip -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $RootDir "ui"), $UiZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host "4. Packaging FastDownloader-Setup.exe..." -ForegroundColor Cyan
& ".\scripts\package-installer.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build FastDownloader-Setup.exe" -ForegroundColor Red
    exit 1
}

Write-Host "All assets built successfully!" -ForegroundColor Green
Get-Item (Join-Path $RootDir "FastDownloader.exe"), $ExtZip, $UiZip, (Join-Path $RootDir "FastDownloader-Setup.exe") | Select-Object Name, Length, LastWriteTime
