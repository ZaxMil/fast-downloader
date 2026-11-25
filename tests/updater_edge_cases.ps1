# Regression tests for apply-update.ps1:
# 1. Competing live listeners fail closed (-1)
# 2. Transient dead-PID overlap returns -3 and resolves on retry
# 3. Failed restart triggers rollback (Restore-OldVersion)
# 4. Successful recovery with SkipRestart + full asset verification
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$helperPath = (Resolve-Path (Join-Path $repoRoot 'scripts\apply-update.ps1')).Path
$helperText = Get-Content $helperPath -Raw

# Extract helper functions (up to the main try block) to unit-test Get-BridgeOwner & Confirm-UpdatedApp
$tryIdx = $helperText.IndexOf("`ntry {")
if ($tryIdx -lt 0) { $tryIdx = $helperText.IndexOf("`r`ntry {") }
if ($tryIdx -lt 0) { throw "Could not locate main try block in apply-update.ps1" }
$functionsBlock = $helperText.Substring(0, $tryIdx)
$functionsBlock = [regex]::Replace($functionsBlock, '(?s)^param\(.*?\)\r?\n', '')

$work = Join-Path $env:TEMP ('FastDownloader-EdgeTests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null

try {
    # --- Test 1 & 2: Get-BridgeOwner + Confirm-UpdatedApp listener states ---
    $statusFile = Join-Path $work 'last-update.json'
    $TargetExe = Join-Path $work 'FastDownloader.exe'
    $NewExe = $TargetExe
    $ExpectedVersion = '0.1.30'
    $RestartTimeoutSeconds = 10
    $SkipRestart = $true
    $TargetPid = 1111
    $ExtensionZip = ''
    $UiZip = ''

    Invoke-Expression $functionsBlock

    # Case A: Two genuinely live processes on port 6801 -> must return -1 and fail closed
    $script:mockLivePids = @(2222, 3333)
    $script:mockConnPids = @(2222, 3333)
    function Get-NetTCPConnection { param($LocalPort, $State, $ErrorAction)
        return @($script:mockConnPids | ForEach-Object { [pscustomobject]@{ OwningProcess = $_ } })
    }
    function Get-Process { param($Id, $ErrorAction)
        if ($script:mockLivePids -contains $Id) {
            return [pscustomobject]@{ Id = $Id; HasExited = $false; Path = $TargetExe }
        }
        return $null
    }

    $ownerLiveConflict = Get-BridgeOwner
    if ($ownerLiveConflict -ne -1) { throw "Expected -1 for two live listeners, got $ownerLiveConflict" }

    $failedClosed = $false
    try { Confirm-UpdatedApp 2222 } catch {
        if ($_.Exception.Message -match 'Ambiguous listeners on port 6801') { $failedClosed = $true }
    }
    if (-not $failedClosed) { throw "Confirm-UpdatedApp did not fail closed on two genuinely live listeners" }

    # Case B: One dead PID (1111) + one live PID (2222) -> Get-BridgeOwner returns -3 (transient)
    $script:mockLivePids = @(2222)
    $script:mockConnPids = @(1111, 2222)
    $ownerTransient = Get-BridgeOwner
    if ($ownerTransient -ne -3) { throw "Expected -3 for dead+live transient overlap, got $ownerTransient" }

    # Case C: Transient -3 clears on 2nd poll -> Confirm-UpdatedApp succeeds
    $script:pollStep = 0
    function Get-NetTCPConnection { param($LocalPort, $State, $ErrorAction)
        $script:pollStep++
        if ($script:pollStep -eq 1) {
            return @([pscustomobject]@{ OwningProcess = 1111 }, [pscustomobject]@{ OwningProcess = 2222 })
        }
        return @([pscustomobject]@{ OwningProcess = 2222 })
    }
    function Invoke-RestMethod { param($Uri, $TimeoutSec, $ErrorAction)
        return [pscustomobject]@{ app = [pscustomobject]@{ currentVersion = '0.1.30'; processId = 2222 } }
    }
    Confirm-UpdatedApp 2222

    # --- Test 3: Failed restart triggers rollback (Restore-OldVersion) ---
    $rbDir = Join-Path $work 'rollback_test'
    $rbInstall = Join-Path $rbDir 'installed'
    $rbStage = Join-Path $rbDir 'staged'
    New-Item -ItemType Directory -Path $rbInstall, $rbStage -Force | Out-Null
    $rbTargetExe = Join-Path $rbInstall 'FastDownloader.exe'
    $rbNewExe = Join-Path $rbStage 'FastDownloader.exe'
    $rbStatus = Join-Path $rbDir 'last-update.json'

    'ORIGINAL_0_1_29_BINARY' | Set-Content -Path $rbTargetExe -NoNewline

    # Compile a tiny dummy executable that has CurrentAppVersion = "0.1.30" but exits immediately without opening port 6801
    $csc = Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:SystemRoot 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
    $badSrc = Join-Path $rbDir 'BadApp.cs'
    @'
using System;
namespace FastDownloader {
    public class UpdateManager { public const string CurrentAppVersion = "0.1.30"; }
    public class BadApp { public static void Main() { Environment.Exit(1); } }
}
'@ | Set-Content -Path $badSrc -Encoding UTF8
    & $csc /nologo /target:exe "/out:$rbNewExe" $badSrc
    if ($LASTEXITCODE -ne 0) { throw "Failed to compile BadApp.cs" }

    # Pad to >50KB to pass size check
    $bytes = [IO.File]::ReadAllBytes($rbNewExe)
    $padded = New-Object byte[] 60000
    [Array]::Copy($bytes, $padded, $bytes.Length)
    [IO.File]::WriteAllBytes($rbNewExe, $padded)

    # Create self-contained extension & UI zips matching ExpectedVersion 0.1.30
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $pkgSrc = Join-Path $work 'pkg_src'
    New-Item -ItemType Directory -Path "$pkgSrc\ext", "$pkgSrc\ui" -Force | Out-Null
    '{"manifest_version":3,"name":"FastDownloader Test","version":"0.1.30"}' | Set-Content "$pkgSrc\ext\manifest.json" -Encoding UTF8
    $randText = (1..40 | ForEach-Object { [guid]::NewGuid().ToString('N') }) -join "`n"
    "// $randText" | Set-Content "$pkgSrc\ext\background.js" -Encoding UTF8
    "<html><body>$randText</body></html>" | Set-Content "$pkgSrc\ui\index.html" -Encoding UTF8
    $extZip = Join-Path $work 'ext.zip'
    $uiZip = Join-Path $work 'ui.zip'
    [System.IO.Compression.ZipFile]::CreateFromDirectory("$pkgSrc\ext", $extZip)
    [System.IO.Compression.ZipFile]::CreateFromDirectory("$pkgSrc\ui", $uiZip)

    $rbExit = 0
    try {
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helperPath `
            -TargetPid 999999 -TargetExe $rbTargetExe -NewExe $rbNewExe `
            -ExtensionZip $extZip -UiZip $uiZip -ExpectedVersion '0.1.30' `
            -StatusFile $rbStatus -RestartTimeoutSeconds 5
        $rbExit = $LASTEXITCODE
    } catch { $rbExit = 1 }

    if ($rbExit -eq 0) { throw "Expected update to fail and roll back when new exe exited immediately" }
    $restoredContent = Get-Content $rbTargetExe -Raw
    if ($restoredContent -ne 'ORIGINAL_0_1_29_BINARY') {
        throw "Rollback failed to restore original executable! Content: $restoredContent"
    }
    $rbRecord = Get-Content $rbStatus -Raw | ConvertFrom-Json
    if ($rbRecord.status -ne 'failed') { throw "Expected status 'failed' after rollback, got $($rbRecord.status)" }

    # --- Test 4: Successful recovery (-SkipRestart) ---
    $recDir = Join-Path $work 'recovery_test'
    $recInstall = Join-Path $recDir 'installed'
    $recStage = Join-Path $recDir 'staged'
    New-Item -ItemType Directory -Path $recInstall, $recStage -Force | Out-Null
    $recTargetExe = Join-Path $recInstall 'FastDownloader.exe'
    $recNewExe = Join-Path $recStage 'FastDownloader.exe'
    $recStatus = Join-Path $recDir 'last-update.json'
    'OLD_BINARY' | Set-Content -Path $recTargetExe -NoNewline
    Copy-Item $rbNewExe $recNewExe -Force

    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helperPath `
        -TargetPid 999999 -TargetExe $recTargetExe -NewExe $recNewExe `
        -ExtensionZip $extZip -UiZip $uiZip -ExpectedVersion '0.1.30' `
        -StatusFile $recStatus -SkipRestart
    if ($LASTEXITCODE -ne 0) {
        $recErr = if (Test-Path $recStatus) { Get-Content $recStatus -Raw } else { '<no status>' }
        throw "Successful recovery test failed with exit code $LASTEXITCODE : $recErr"
    }
    $recRecord = Get-Content $recStatus -Raw | ConvertFrom-Json
    if ($recRecord.status -ne 'success') { throw "Expected status 'success', got $($recRecord.status)" }

    Write-Host "PASS: competing listeners, transient dead-PID retry, failed restart rollback, and recovery all verified."
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
