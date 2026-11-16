param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\FastDownloader Pro')
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$quote = [char]34
$targetExe = Join-Path $InstallDirectory 'FastDownloader.exe'
if (-not (Test-Path $targetExe)) { throw "FastDownloader was not found at $targetExe" }
$targetExe = [IO.Path]::GetFullPath($targetExe)
$running = @(
    Get-Process -Name 'FastDownloader' -ErrorAction SilentlyContinue |
    Where-Object {
        try { [string]::Equals([IO.Path]::GetFullPath($_.Path), $targetExe, [StringComparison]::OrdinalIgnoreCase) }
        catch { $false }
    }
)
if ($running.Count -ne 1) {
    throw "Expected exactly one running FastDownloader process at $targetExe. Open the installed app and retry."
}
$api = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/update/status' -TimeoutSec 5
$targetPid = [int]$api.app.processId
if ($targetPid -ne $running[0].Id) {
    throw "Local API PID $targetPid does not match installed app PID $($running[0].Id). Refusing to update the wrong process."
}
$taskReply = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/tasks' -TimeoutSec 5
$activeStatuses = @('active','waiting','downloading','processing')
$activeTasks = @($taskReply.tasks | Where-Object { $activeStatuses -contains ([string]$_.status).ToLowerInvariant() })
if ($activeTasks.Count -gt 0) {
    throw "Update stopped safely because $($activeTasks.Count) download(s) are active or waiting. Pause or finish them, then retry."
}
$work = Join-Path $env:TEMP ('FastDownloaderBootstrap_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$logRoot = Join-Path $env:LOCALAPPDATA 'FastDownloader'
$statusFile = Join-Path $logRoot 'last-update.json'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
try {
    $headers = @{ 'User-Agent' = 'FastDownloader-InPlace-Recovery'; 'Accept' = 'application/vnd.github+json' }
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/ZaxMil/fast-downloader/releases/latest' -Headers $headers -TimeoutSec 20
    $expectedVersion = ([string]$release.tag_name).TrimStart('v','V')
    if ($expectedVersion -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid latest release tag: $($release.tag_name)" }
    function Get-VerifiedReleaseAsset([string]$name) {
        $asset = $release.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if (-not $asset -or -not $asset.browser_download_url) { throw "Release asset is missing: $name" }
        if (-not $asset.digest -or $asset.digest -notmatch '^sha256:([0-9a-fA-F]{64})$') {
            throw "Release asset has no valid SHA-256 digest: $name"
        }
        $destination = Join-Path $work $name
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $destination -Headers @{ 'User-Agent' = 'FastDownloader-InPlace-Recovery' } -TimeoutSec 120
        $actual = (Get-FileHash -Path $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        $expected = $Matches[1].ToLowerInvariant()
        if ($actual -ne $expected) { throw "SHA-256 verification failed for $name" }
        return $destination
    }
    $newExe = Get-VerifiedReleaseAsset 'FastDownloader.exe'
    $extensionZip = Get-VerifiedReleaseAsset 'fast-downloader-extension.zip'
    $uiZip = Get-VerifiedReleaseAsset 'fast-downloader-ui.zip'
    $helperPath = Get-VerifiedReleaseAsset 'apply-update.ps1'
    $argumentLine = @(
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy Bypass',
        ('-File ' + $quote + $helperPath + $quote),
        "-TargetPid $targetPid",
        ('-TargetExe ' + $quote + $targetExe + $quote),
        ('-NewExe ' + $quote + $newExe + $quote),
        ('-ExtensionZip ' + $quote + $extensionZip + $quote),
        ('-UiZip ' + $quote + $uiZip + $quote),
        ('-ExpectedVersion ' + $quote + $expectedVersion + $quote),
        ('-StatusFile ' + $quote + $statusFile + $quote)
    ) -join ' '
    Write-Host "Applying FastDownloader $expectedVersion in place. Your existing install folder and settings will be preserved."
    $helper = Start-Process -FilePath 'powershell.exe' -ArgumentList $argumentLine -Wait -PassThru -WindowStyle Hidden
    if ($helper.ExitCode -ne 0) {
        $log = Join-Path $logRoot 'update.log'
        $tail = if (Test-Path $log) { (Get-Content $log -Tail 25) -join [Environment]::NewLine } else { 'No updater log was created.' }
        throw ("In-place update helper failed with exit code $($helper.ExitCode)." + [Environment]::NewLine + $tail)
    }
    $result = Get-Content $statusFile -Raw | ConvertFrom-Json
    if ($result.status -ne 'success' -or $result.expectedVersion -ne $expectedVersion) {
        throw "Update helper did not confirm version $expectedVersion. Status: $($result.status); $($result.message)"
    }
    Remove-Item $work -Recurse -Force
    Write-Host "SUCCESS: FastDownloader $expectedVersion is installed and its local API was verified."
}
catch {
    $errorPath = Join-Path $logRoot 'bootstrap-update-error.log'
    $_.ToString() | Set-Content -Path $errorPath -Encoding UTF8
    Write-Error "$($_.Exception.Message) Recovery files were kept at: $work"
    exit 1
}
