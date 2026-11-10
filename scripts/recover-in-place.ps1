# One-time repair for installations whose older embedded update helper cannot
# complete an upgrade. For Windows PowerShell 5.1. No new installer required.
# Safe by design: user must exit FastDownloader completely before running.
param(
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [string]$ExpectedVersion = '0.1.18'
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$TargetExe = [IO.Path]::GetFullPath($TargetExe)
if (-not (Test-Path $TargetExe -PathType Leaf)) {
    throw "FastDownloader executable not found: $TargetExe"
}
if ((Split-Path $TargetExe -Leaf) -ne 'FastDownloader.exe') {
    throw 'Only the existing FastDownloader.exe may be updated.'
}

# Never interfere with active transfers or terminate any application process.
$live = @(Get-CimInstance Win32_Process -Filter "Name = 'FastDownloader.exe'" | Where-Object {
    $_.ExecutablePath -and [string]::Equals(
        [IO.Path]::GetFullPath($_.ExecutablePath), $TargetExe,
        [StringComparison]::OrdinalIgnoreCase)
})
if ($live.Count -gt 0) {
    throw 'FastDownloader is still running. Finish/pause downloads, exit it from the tray, then retry. No files were changed.'
}
try {
    $listening = @(Get-NetTCPConnection -State Listen -LocalPort 6801 -ErrorAction SilentlyContinue)
    if ($listening.Count -gt 0) { throw 'Port 6801 is already in use. Close other FastDownloader instances first.' }
} catch {
    if ($_.Exception.Message -match 'already in use') { throw }
}

# Fetch official release metadata, not user-submitted URLs or arbitrary binaries.
$releaseUrl = "https://api.github.com/repos/ZaxMil/fast-downloader/releases/tags/v$ExpectedVersion"
$headers = @{ 'User-Agent' = 'FastDownloader-Recovery'; 'Accept' = 'application/vnd.github+json' }
$release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers -TimeoutSec 25
if ($release.tag_name -ne "v$ExpectedVersion" -or $release.draft -or $release.prerelease) {
    throw 'Official stable release metadata did not match the requested version.'
}
$stage = Join-Path $env:TEMP ('FastDownloader_Recovery_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$required = @(
    'FastDownloader.exe',
    'fast-downloader-extension.zip',
    'fast-downloader-ui.zip',
    'apply-update.ps1'
)
$downloads = @{}
foreach ($name in $required) {
    $asset = @($release.assets | Where-Object { $_.name -eq $name }) | Select-Object -First 1
    if (-not $asset -or -not $asset.digest -or
        -not ($asset.digest -match '^sha256:[a-fA-F0-9]{64}$')) {
        throw "Missing official SHA-256-verified asset: $name"
    }
    $dest = Join-Path $stage $name
    Invoke-WebRequest -Uri $asset.browser_download_url -Headers @{ 'User-Agent' = 'FastDownloader-Recovery' } -OutFile $dest -UseBasicParsing -TimeoutSec 180
    $hash = (Get-FileHash -Path $dest -Algorithm SHA256).Hash
    if (-not $hash.Equals($asset.digest.Substring(7), [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA-256 mismatch: $name"
    }
    $downloads[$name] = $dest
}

# The new helper itself is verified against the official release SHA-256.
# The old installed application binary, extension, UI and settings are backed
# up by that helper; no installer is launched and user downloads are untouched.
$status = Join-Path (Join-Path $env:LOCALAPPDATA 'FastDownloader') 'last-update.json'
$helperArgs = @(
    '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
    '-File', $downloads['apply-update.ps1'],
    '-TargetPid', '2147483647',
    '-TargetExe', $TargetExe,
    '-NewExe', $downloads['FastDownloader.exe'],
    '-ExtensionZip', $downloads['fast-downloader-extension.zip'],
    '-UiZip', $downloads['fast-downloader-ui.zip'],
    '-ExpectedVersion', $ExpectedVersion,
    '-StatusFile', $status
)
Write-Host "Verified official release v$ExpectedVersion. Starting backup, in-place update and restart."
& powershell.exe @helperArgs
if ($LASTEXITCODE -ne 0) {
    $diagnostics = if (Test-Path $status) { Get-Content $status -Raw } else { '(no update status)' }
    throw "Recovery failed and rollback was attempted. Check the log in LocalAppData/FastDownloader. $diagnostics"
}
$api = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/update/status' -TimeoutSec 5
if ($api.app.currentVersion -ne $ExpectedVersion -or [int]$api.app.processId -le 0) {
    throw 'The recovery helper finished but the running application could not be verified.'
}
$installedExt = Join-Path (Split-Path $TargetExe -Parent) 'extension/manifest.json'
$manifest = Get-Content $installedExt -Raw | ConvertFrom-Json
if ($manifest.version -ne $ExpectedVersion) {
    throw 'Executable updated, but the browser extension folder has a different version.'
}
Write-Host "SUCCESS: FastDownloader v$ExpectedVersion is running at PID $($api.app.processId)."
