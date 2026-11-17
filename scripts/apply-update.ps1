param(
    [Parameter(Mandatory=$true)][int]$TargetPid,
    [Parameter(Mandatory=$true)][string]$TargetExe,
    [Parameter(Mandatory=$true)][string]$NewExe,
    [Parameter(Mandatory=$true)][AllowEmptyString()][string]$ExtensionZip,
    [Parameter(Mandatory=$true)][AllowEmptyString()][string]$UiZip,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [Parameter(Mandatory=$true)][string]$StatusFile,
    [string]$AssetStateFile = '',
    [ValidateRange(5, 300)][int]$RestartTimeoutSeconds = 120,
    [switch]$SkipRestart,
    [switch]$RestartOnly
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
function Get-TreeFingerprint([string]$directory) {
    if (-not (Test-Path $directory)) { return '' }
    $rootPath = [IO.Path]::GetFullPath($directory).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $material = New-Object Text.StringBuilder
    $files = @(Get-ChildItem -LiteralPath $rootPath -File -Recurse | Sort-Object FullName)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($rootPath.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar).Replace('\', '/')
        $digest = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        [void]$material.Append($relative).Append([char]0).Append($digest).Append([char]10)
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($material.ToString())
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') }
    finally { $sha.Dispose() }
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
    # Once the target process is gone, its old PID may briefly remain in the
    # TCP table. Do not wait on that stale row; the new app's API PID/version
    # verification and rollback below are the final authority.
    $deadline = [DateTime]::UtcNow.AddSeconds(18)
    $lastOwnerDescription = 'No listener observed'
    $lastLogged = ''
    do {
        $owner = Get-BridgeOwner
        if ($owner -eq 0) {
            Log 'Port 6801 is free for the updated app.'
            return
        }
        if ($owner -eq -2) {
            Log 'TCP listener ownership unavailable; continuing with strict API process identity verification.'
            return
        }
        if ($owner -eq -1) { throw 'Multiple processes listen on port 6801. Refusing update.' }

        $ownerProcess = Get-Process -Id $owner -ErrorAction SilentlyContinue
        $ownerPath = $null
        if ($ownerProcess) { try { $ownerPath = $ownerProcess.Path } catch { } }
        $lastOwnerDescription = "PID $owner; path " + $(if ($ownerPath) { $ownerPath } else { '<process not found or path unavailable>' })
        if ($lastOwnerDescription -ne $lastLogged) {
            Log "Port 6801 listener observed: $lastOwnerDescription"
            $lastLogged = $lastOwnerDescription
        }

        if ($owner -eq $TargetPid) {
            if (-not $ownerProcess) {
                Log "TCP table still reports stopped target PID $TargetPid; proceeding to strict updated API verification."
                return
            }
            if ($ownerPath -and -not [string]::Equals(
                [IO.Path]::GetFullPath($ownerPath),
                [IO.Path]::GetFullPath($TargetExe),
                [StringComparison]::OrdinalIgnoreCase)) {
                throw "Target PID $TargetPid was reused by '$ownerPath'; refusing to replace files."
            }
            Start-Sleep -Milliseconds 450
            continue
        }

        if ($ownerPath -and [string]::Equals(
            [IO.Path]::GetFullPath($ownerPath),
            [IO.Path]::GetFullPath($TargetExe),
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "Another FastDownloader process PID $owner still owns port 6801."
        }
        throw "Port 6801 is owned by unrelated process PID $owner ($lastOwnerDescription). Refusing update."
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Port 6801 remained active after the old app exited. Last listener: $lastOwnerDescription."
}
function Confirm-UpdatedApp([int]$newPid) {
    $deadline = [DateTime]::UtcNow.AddSeconds($RestartTimeoutSeconds)
    $lastObservation = 'No response from FastDownloader HTTP bridge'
    $lastLogged = ''
    $staleApiObservations = 0
    while ([DateTime]::UtcNow -lt $deadline) {
        $p = Get-Process -Id $newPid -ErrorAction SilentlyContinue
        $owner = Get-BridgeOwner
        if ($owner -eq -1) { throw 'Ambiguous listeners on port 6801 while verifying restart.' }
        $ownerProcess = if ($owner -gt 0) { Get-Process -Id $owner -ErrorAction SilentlyContinue } else { $null }
        $ownerPath = $null
        if ($ownerProcess) { try { $ownerPath = $ownerProcess.Path } catch { } }
        if ($ownerPath -and -not [string]::Equals(
            [IO.Path]::GetFullPath($ownerPath),
            [IO.Path]::GetFullPath($TargetExe),
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "Port 6801 is owned by unrelated PID $owner ($ownerPath). Refusing to treat it as FastDownloader."
        }
        try {
            $reply = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/update/status' -TimeoutSec 3 -ErrorAction Stop
            $runningVersion = [string]$reply.app.currentVersion
            $reportedPid = [int]$reply.app.processId
            $reportedProcess = Get-Process -Id $reportedPid -ErrorAction SilentlyContinue
            $reportedPath = $null
            if ($reportedProcess) { try { $reportedPath = $reportedProcess.Path } catch { } }
            $reportedPathMatches = $reportedPath -and [string]::Equals(
                [IO.Path]::GetFullPath($reportedPath),
                [IO.Path]::GetFullPath($TargetExe),
                [StringComparison]::OrdinalIgnoreCase)
            if ($runningVersion -eq $ExpectedVersion -and $reportedPid -eq $newPid -and $reportedPathMatches) {
                Log "Verified updated API version $runningVersion on launched PID $reportedPid."
                return
            }
            # Starting a second copy can be redirected by the app's single-instance
            # mutex. Accept that only when the API itself reports the expected
            # version and its live process is the verified installed executable.
            if ($runningVersion -eq $ExpectedVersion -and $reportedPid -ne $newPid -and $reportedPathMatches) {
                Log "Verified expected version $runningVersion through existing installed PID $reportedPid; launch PID $newPid was redirected by the single-instance guard."
                return
            }
            $lastObservation = "API returned version '$runningVersion' and PID $reportedPid; expected $ExpectedVersion from installed executable '$TargetExe'. Launch PID $newPid is " + $(if ($p -and -not $p.HasExited) { 'still running' } else { 'no longer running' }) + '.'
            if ($reportedPid -gt 0 -and $reportedPid -ne $newPid -and $reportedPathMatches) {
                $staleApiObservations++
                if ($staleApiObservations -ge 5) { throw "A different FastDownloader process PID $reportedPid is still serving version '$runningVersion'. $lastObservation" }
            } else { $staleApiObservations = 0 }
        } catch {
            if ($_.Exception.Message -like 'A different FastDownloader process PID*') { throw }
            $lastObservation = "API not ready: $($_.Exception.Message)"
        }
        if ($lastObservation -ne $lastLogged) {
            Log $lastObservation
            $lastLogged = $lastObservation
        }
        if ((-not $p -or $p.HasExited) -and $owner -eq 0) {
            throw "Launched FastDownloader PID $newPid exited and no local API is listening. $lastObservation"
        }
        Start-Sleep -Milliseconds 900
    }
    $bridgeError = Join-Path $root 'bridge_error.log'
    if (Test-Path $bridgeError) {
        Log ('Bridge log: ' + ((Get-Content $bridgeError -Tail 8) -join ' | '))
    }
    throw "Restart validation timed out after $RestartTimeoutSeconds seconds. $lastObservation"
}
function Confirm-RestoredApp([int]$restoredPid) {
    $deadline = [DateTime]::UtcNow.AddSeconds($RestartTimeoutSeconds)
    $lastObservation = 'No response from restored FastDownloader HTTP bridge'
    while ([DateTime]::UtcNow -lt $deadline) {
        $p = Get-Process -Id $restoredPid -ErrorAction SilentlyContinue
        if (-not $p -or $p.HasExited) { throw "Restored process PID $restoredPid exited before its API was ready." }
        try {
            $reply = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/update/status' -TimeoutSec 3 -ErrorAction Stop
            $reportedPid = [int]$reply.app.processId
            $runningVersion = [string]$reply.app.currentVersion
            if ($reportedPid -eq $restoredPid -and -not [string]::IsNullOrWhiteSpace($runningVersion)) {
                Log "Verified restored app version $runningVersion on PID $reportedPid."
                return
            }
            $lastObservation = "Restored API reported PID $reportedPid and version '$runningVersion'; expected PID $restoredPid."
        } catch { $lastObservation = "Restored API not ready: $($_.Exception.Message)" }
        Start-Sleep -Milliseconds 900
    }
    throw "Restored app validation timed out. $lastObservation"
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
        Wait-BridgeRelease
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
    if ($RestartOnly) {
        if (-not (Test-Path $TargetExe)) { throw "Installed executable is missing: $TargetExe" }
        Wait-BridgeRelease
        Status 'restarting' 'Launching the installed app and verifying its process and local API'
        $started = TryRestart $TargetExe
        if (-not $started) { throw 'Failed to launch the installed application.' }
        Confirm-UpdatedApp $started.Id
        Status 'success' "FastDownloader $ExpectedVersion restarted and API verified (PID $($started.Id))"
        exit 0
    }

    if (-not (Test-Path $NewExe) -or (Get-Item $NewExe).Length -lt 50000) { throw 'Missing or incomplete application executable.' }
    Assert-UpdateBinaryVersion
    if (-not $SkipRestart) { Wait-BridgeRelease }
    foreach ($zip in @($ExtensionZip, $UiZip)) {
        if (-not [string]::IsNullOrWhiteSpace($zip) -and (-not (Test-Path $zip) -or (Get-Item $zip).Length -lt 500)) { throw "Missing or incomplete update package: $zip" }
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($ExtensionZip)) {
        Expand-Archive -Path $ExtensionZip -DestinationPath (Join-Path $stage 'extension') -Force
        $extManifest = Join-Path $stage 'extension/manifest.json'
        if (-not (Test-Path $extManifest)) { throw 'Extension manifest missing from package.' }
        $manifest = Get-Content $extManifest -Raw | ConvertFrom-Json
        if ($manifest.version -ne $ExpectedVersion) { throw 'Extension version does not match the application release.' }
        if (-not (Test-Path (Join-Path $stage 'extension/background.js'))) { throw 'Extension background.js missing.' }
    }
    if (-not [string]::IsNullOrWhiteSpace($UiZip)) {
        Expand-Archive -Path $UiZip -DestinationPath (Join-Path $stage 'ui') -Force
        if (-not (Test-Path (Join-Path $stage 'ui/index.html'))) { throw 'Application interface missing.' }
    }

    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    $originalExe = Test-Path $TargetExe
    $originalExtension = Test-Path (Join-Path $root 'extension')
    $originalUi = Test-Path (Join-Path $root 'ui')
    if ($originalExe) { Copy-Item $TargetExe (Join-Path $backup 'FastDownloader.exe') -Force }
    if ($originalExtension -and -not [string]::IsNullOrWhiteSpace($ExtensionZip)) { Copy-Item (Join-Path $root 'extension') (Join-Path $backup 'extension') -Recurse -Force }
    if ($originalUi -and -not [string]::IsNullOrWhiteSpace($UiZip)) { Copy-Item (Join-Path $root 'ui') (Join-Path $backup 'ui') -Recurse -Force }

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
        if (($folder -eq 'extension' -and [string]::IsNullOrWhiteSpace($ExtensionZip)) -or ($folder -eq 'ui' -and [string]::IsNullOrWhiteSpace($UiZip))) { continue }
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
    if (-not [string]::IsNullOrWhiteSpace($AssetStateFile) -and (Test-Path $AssetStateFile)) {
        try {
            $stateDir = Join-Path $env:LOCALAPPDATA 'FastDownloader'
            New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
            $stateTarget = Join-Path $stateDir 'installed-assets.json'
            $stateTemp = $stateTarget + '.tmp'
            $state = Get-Content -LiteralPath $AssetStateFile -Raw | ConvertFrom-Json
            $installedTrees = @{
                extension = Get-TreeFingerprint (Join-Path $root 'extension')
                ui = Get-TreeFingerprint (Join-Path $root 'ui')
            }
            if ($state.PSObject.Properties.Name -contains 'installedTrees') { $state.installedTrees = $installedTrees }
            else { $state | Add-Member -NotePropertyName installedTrees -NotePropertyValue $installedTrees }
            $state | ConvertTo-Json -Depth 8 -Compress | Set-Content -LiteralPath $stateTemp -Encoding UTF8
            Move-Item $stateTemp $stateTarget -Force
            Log 'Committed installed asset fingerprints after successful restart verification.'
        } catch { Log "Could not save optional asset fingerprints: $($_.Exception.Message)" }
    }
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
                if (-not $restored) { throw 'Previous application could not be relaunched after rollback.' }
                Confirm-RestoredApp $restored.Id
                Log "Previous application API verified on PID $($restored.Id)."
            }
        }
    } catch {
        Log "Rollback also failed: $($_.Exception.Message)"
        $reason = "$reason; rollback failed: $($_.Exception.Message)"
    }
    Status 'failed' $reason
    exit 1
}
