param(
    [Parameter(Mandatory=$true)][int]$TargetPid,
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [Parameter(Mandatory=$true)][string]$NewExe,
    [Parameter(Mandatory=$true)][string]$ExtensionZip,
    [Parameter(Mandatory=$true)][string]$UiZip,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [Parameter(Mandatory=$true)][string]$StatusFile,
    [switch]$SkipRestart
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = Split-Path -Parent $TargetExe
$work = Split-Path -Parent $NewExe
$log = Join-Path (Split-Path -Parent $StatusFile) 'update.log'
$backup = Join-Path $work 'backup'
$stage = Join-Path $work 'staged'
$started = $null
$installed = $false
$originalExe = $false
$originalExtension = $false
$originalUi = $false

function Log([string]$message) {
    $directory = Split-Path -Parent $StatusFile
    if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    Add-Content -Path $log -Value ("{0} {1}" -f [DateTime]::UtcNow.ToString('o'), $message)
}
function Status([string]$state, [string]$message) {
    $record = @{ status = $state; message = $message; expectedVersion = $ExpectedVersion; timestamp = [DateTime]::UtcNow.ToString('o') }
    $record | ConvertTo-Json -Compress | Set-Content -Path $StatusFile -Encoding UTF8
    Log "$state - $message"
}
function TryRestart([string]$program) {
    if ($SkipRestart -or -not (Test-Path $program)) { return $null }
    return Start-Process -FilePath $program -WorkingDirectory (Split-Path -Parent $program) -PassThru
}
function Restore-OldVersion {
    if ($started -and -not $started.HasExited) {
        Stop-Process -Id $started.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }
    if ($originalExe -and (Test-Path (Join-Path $backup 'FastDownloader.exe'))) {
        Copy-Item (Join-Path $backup 'FastDownloader.exe') $TargetExe -Force
    }
    foreach ($name in @('extension','ui')) {
        $wasPresent = if ($name -eq 'extension') { $originalExtension } else { $originalUi }
        $targetFolder = Join-Path $root $name
        $saved = Join-Path $backup $name
        if ($wasPresent -and (Test-Path $saved)) {
            if (Test-Path $targetFolder) { Remove-Item $targetFolder -Recurse -Force }
            Copy-Item $saved $targetFolder -Recurse -Force
        } elseif (-not $wasPresent -and (Test-Path $targetFolder)) {
            Remove-Item $targetFolder -Recurse -Force
        }
    }
}
try {
    Status 'waiting' 'Waiting for the old application process to close'
    $deadline = [DateTime]::UtcNow.AddSeconds(35)
    while (Get-Process -Id $TargetPid -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -ge $deadline) { throw "The running application did not exit within 35 seconds. Please close it from the system tray and use the installer." }
        Start-Sleep -Milliseconds 350
    }
    if (-not (Test-Path $NewExe) -or (Get-Item $NewExe).Length -lt 50000) { throw 'Missing or incomplete application executable.' }
    foreach ($zip in @($ExtensionZip, $UiZip)) {
        if (-not (Test-Path $zip) -or (Get-Item $zip).Length -lt 500) { throw "Missing or incomplete update package: $zip" }
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    Expand-Archive -Path $ExtensionZip -DestinationPath (Join-Path $stage 'extension') -Force
    Expand-Archive -Path $UiZip -DestinationPath (Join-Path $stage 'ui') -Force
    $extManifest = Join-Path $stage 'extension/manifest.json'
    if (-not (Test-Path $extManifest)) { throw 'Extension manifest missing from package.' }
    $manifest = Get-Content $extManifest -Raw | ConvertFrom-Json
    if ($manifest.version -ne $ExpectedVersion) { throw 'Extension version does not match the application release.' }
    if (-not (Test-Path (Join-Path $stage 'extension/background.js'))) { throw 'Extension background.js missing.' }
    if (-not (Test-Path (Join-Path $stage 'ui/index.html'))) { throw 'Application interface missing.' }

    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    $originalExe = Test-Path $TargetExe
    $originalExtension = Test-Path (Join-Path $root 'extension')
    $originalUi = Test-Path (Join-Path $root 'ui')
    if ($originalExe) { Copy-Item $TargetExe (Join-Path $backup 'FastDownloader.exe') -Force }
    if ($originalExtension) { Copy-Item (Join-Path $root 'extension') (Join-Path $backup 'extension') -Recurse -Force }
    if ($originalUi) { Copy-Item (Join-Path $root 'ui') (Join-Path $backup 'ui') -Recurse -Force }

    Status 'installing' 'Applying verified update files'
    $copied = $false
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        try {
            Copy-Item $NewExe $TargetExe -Force -ErrorAction Stop
            $copied = $true
            break
        } catch {
            Log "File replace attempt $attempt/12: $($_.Exception.Message)"
            Start-Sleep -Milliseconds 750
        }
    }
    if (-not $copied) { throw 'Unable to replace the application executable after 12 attempts.' }
    foreach ($folder in @('extension','ui')) {
        $targetFolder = Join-Path $root $folder
        if (-not (Test-Path $targetFolder)) { New-Item -ItemType Directory $targetFolder | Out-Null }
        Copy-Item (Join-Path $stage "$folder/*") $targetFolder -Recurse -Force -ErrorAction Stop
    }
    $installed = $true
    if ($SkipRestart) {
        Status 'success' "Update $ExpectedVersion files installed (restart skipped for test)"
        exit 0
    }

    Status 'restarting' 'Launching updated application and checking its version'
    $started = TryRestart $TargetExe
    if (-not $started) { throw 'Failed to launch updated application.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    $verified = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 700
        try {
            $response = Invoke-RestMethod -Uri "http://127.0.0.1:6801/api/update/status" -TimeoutSec 2
            if ($response.app.currentVersion -eq $ExpectedVersion) {
                $verified = $true
                break
            }
        } catch { }
        if ($started.HasExited) { throw 'Updated application exited before becoming ready.' }
    }
    if (-not $verified) { throw "Updated app did not report version $ExpectedVersion within 40 seconds." }
    Status 'success' "FastDownloader $ExpectedVersion launched and verified"
    exit 0
}
catch {
    $reason = $_.Exception.Message
    Log "Update failed: $reason"
    try {
        if ($originalExe -and (Test-Path $backup)) {
            Restore-OldVersion
            [void](TryRestart $TargetExe)
        }
    } catch {
        Log "Rollback also failed: $($_.Exception.Message)"
        $reason = "$reason; rollback failed: $($_.Exception.Message)"
    }
    Status 'failed' $reason
    exit 1
}
