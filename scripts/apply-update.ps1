param(
    [Parameter(Mandatory=$true)][int]$TargetPid,
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [Parameter(Mandatory=$true)][string]$NewExe,
    [Parameter(Mandatory=$true)][string]$ExtensionZip,
    [Parameter(Mandatory=$true)][string]$UiZip,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [Parameter(Mandatory=$true)][string]$StatusFile,
    [ValidateRange(5, 300)][int]$RestartTimeoutSeconds = 120,
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
    Log ("Launching verified executable: " + $program)
    return Start-Process -FilePath $program -WorkingDirectory (Split-Path -Parent $program) -PassThru
}
function Test-ExpectedProcess([int]$id, [string]$expectedPath) {
    if ($id -le 0) { return $false }
    $p = Get-Process -Id $id -ErrorAction SilentlyContinue
    if (-not $p -or $p.HasExited) { return $false }
    $real = try { $p.Path } catch { $null }
    return $real -and [string]::Equals(
        [IO.Path]::GetFullPath($real),
        [IO.Path]::GetFullPath($expectedPath),
        [StringComparison]::OrdinalIgnoreCase)
}
function Get-BridgeOwner {
    try {
        $listeners = @(Get-NetTCPConnection -LocalPort 6801 -State Listen -ErrorAction Stop)
        $ids = @($listeners | ForEach-Object { [int]$_.OwningProcess } | Where-Object { $_ -gt 0 } | Select-Object -Unique)
        if ($ids.Count -eq 0) { return 0 }
        if ($ids.Count -eq 1) { return $ids[0] }
        return -1 # Multiple listeners: fail closed rather than verify the wrong app.
    } catch {
        if ($_.Exception.Message -match 'No MSFT_NetTCPConnection objects found') { return 0 }
        # If network diagnostics are blocked, the API's processId is mandatory.
        return -2
    }
}
function Assert-UpdateBinaryVersion {
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($NewExe))
    $type = $assembly.GetType('FastDownloader.UpdateManager')
    if (-not $type) { throw 'New executable does not contain FastDownloader.UpdateManager.' }
    $field = $type.GetField('CurrentAppVersion', [Reflection.BindingFlags]'Public,Static')
    if (-not $field) { throw 'New executable is missing its application version.' }
    $actualVersion = [string]$field.GetRawConstantValue()
    if ($actualVersion -ne $ExpectedVersion) {
        throw "New EXE has version $actualVersion, not expected $ExpectedVersion."
    }
    Log "Verified staged executable version $actualVersion."
}
function Wait-BridgeRelease {
    # Closing the PID must also release the old API listener. ReuseAddress can
    # otherwise allow the new dashboard to reach a stale/wrong process.
    $deadline = [DateTime]::UtcNow.AddSeconds(18)
    do {
        $owner = Get-BridgeOwner
        if ($owner -eq 0) { return }
        if ($owner -eq -2) {
            Log 'TCP listener ownership unavailable; continuing with strict API process identity verification.'
            return
        }
        if ($owner -eq -1) { throw 'Multiple processes listen on port 6801. Refusing update.' }
        if ($owner -ne $TargetPid) {
            throw "Port 6801 belongs to unrelated process PID $owner. Update aborted before replacement."
        }
        Start-Sleep -Milliseconds 450
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Old FastDownloader process exited but port 6801 is still in use.'
}
function Confirm-UpdatedApp([int]$newPid) {
    $deadline = [DateTime]::UtcNow.AddSeconds($RestartTimeoutSeconds)
    $lastObservation = 'No response from FastDownloader HTTP bridge'
    $lastLogged = ''
    while ([DateTime]::UtcNow -lt $deadline) {
        $p = Get-Process -Id $newPid -ErrorAction SilentlyContinue
        if (-not $p -or $p.HasExited) {
            throw "New FastDownloader process PID $newPid exited before the API was ready. $lastObservation"
        }
        $owner = Get-BridgeOwner
        if ($owner -gt 0 -and $owner -ne $newPid) {
            throw "Port 6801 is held by PID $owner, not the newly started FastDownloader PID $newPid."
        }
        if ($owner -eq -1) { throw 'Ambiguous listeners on port 6801 while verifying restart.' }
        try {
            $reply = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/update/status' -TimeoutSec 3 -ErrorAction Stop
            $runningVersion = [string]$reply.app.currentVersion
            $reportedPid = [int]$reply.app.processId
            if ($runningVersion -eq $ExpectedVersion -and $reportedPid -eq $newPid) {
                Log "Verified updated API version $runningVersion on PID $reportedPid."
                return
            }
            $lastObservation = "API returned version '$runningVersion' and PID $reportedPid; expected $ExpectedVersion from PID $newPid."
        } catch {
            $lastObservation = "API not ready: $($_.Exception.Message)"
        }
        if ($lastObservation -ne $lastLogged) {
            Log $lastObservation
            $lastLogged = $lastObservation
        }
        Start-Sleep -Milliseconds 900
    }
    $bridgeError = Join-Path $root 'bridge_error.log'
    if (Test-Path $bridgeError) {
        Log ('Bridge log: ' + ((Get-Content $bridgeError -Tail 8) -join ' | '))
    }
    throw "Restart validation timed out after $RestartTimeoutSeconds seconds. $lastObservation"
}
function Restore-OldVersion {
    if ($started -and -not $started.HasExited) {
        if (-not (Test-ExpectedProcess $started.Id $TargetExe)) {
            throw "Rollback aborted: PID $($started.Id) does not match installed FastDownloader."
        }
        Stop-Process -Id $started.Id -Force -ErrorAction Stop
        Start-Sleep -Milliseconds 800
        $started.Refresh()
        if (-not $started.HasExited) { throw 'Rollback aborted: updated process could not be stopped.' }
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
    # An old hung GUI/tray process can hold the executable open forever.
    # Wait for graceful shutdown, then terminate ONLY the exact original PID
    # after verifying its executable path (never kill all FastDownloader processes).
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ($true) {
        $old = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
        if (-not $old) { break }
        if ([DateTime]::UtcNow -ge $deadline) {
            if ($SkipRestart) { throw 'Test-mode process is still running' }
            $actualPath = try { $old.Path } catch { $null }
            if (-not $actualPath -or -not [string]::Equals([IO.Path]::GetFullPath($actualPath), [IO.Path]::GetFullPath($TargetExe), [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Original PID does not match the expected executable. Refusing to terminate an unrelated process.'
            }
            Status 'closing' 'The old app did not exit; closing its verified process to finish the update'
            try { [void]$old.CloseMainWindow() } catch {}
            Start-Sleep -Seconds 2
            $old = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
            if ($old) {
                $actualPath = try { $old.Path } catch { $null }
                if (-not $actualPath -or -not [string]::Equals([IO.Path]::GetFullPath($actualPath), [IO.Path]::GetFullPath($TargetExe), [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Process identity changed while waiting. Refusing to force close it.'
                }
                Stop-Process -Id $TargetPid -Force -ErrorAction Stop
                Start-Sleep -Seconds 1
            }
            if (Get-Process -Id $TargetPid -ErrorAction SilentlyContinue) { throw 'Old app still running after a targeted close. Use the installer.' }
            break
        }
        Start-Sleep -Milliseconds 300
    }
    if (-not (Test-Path $NewExe) -or (Get-Item $NewExe).Length -lt 50000) { throw 'Missing or incomplete application executable.' }
    Assert-UpdateBinaryVersion
    if (-not $SkipRestart) { Wait-BridgeRelease }
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

    Status 'restarting' "Starting version $ExpectedVersion and verifying its process and local API"
    $started = TryRestart $TargetExe
    if (-not $started) { throw 'Failed to launch updated application.' }
    Confirm-UpdatedApp $started.Id
    Status 'success' "FastDownloader $ExpectedVersion running and API verified (PID $($started.Id))"
    exit 0
}
catch {
    $reason = $_.Exception.Message
    Log "Update failed: $reason"
    try {
        if ($originalExe -and (Test-Path $backup)) {
            Log 'Restoring previous executable and companion assets after failed validation.'
            Restore-OldVersion
            if (-not $SkipRestart) {
                $restored = TryRestart $TargetExe
                if ($restored) { Log "Previous application restarted as PID $($restored.Id)." }
            }
        }
    } catch {
        Log "Rollback also failed: $($_.Exception.Message)"
        $reason = "$reason; rollback failed: $($_.Exception.Message)"
    }
    Status 'failed' $reason
    exit 1
}
