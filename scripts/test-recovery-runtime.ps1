$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $Root 'FastDownloader.exe'
if (-not (Test-Path $exe)) { throw 'Compile the native app before running recovery logic tests.' }
$asm = [Reflection.Assembly]::LoadFrom($exe)
$engine = $asm.GetType('FastDownloader.EngineManager', $true)
$updater = $asm.GetType('FastDownloader.UpdateManager', $true)
$flags = [Reflection.BindingFlags]'NonPublic,Static'
function Invoke-Helper($type, $name, [object[]]$parameters) {
    $method = $type.GetMethod($name, $flags)
    if (-not $method) { throw "Helper method missing: $name" }
    return $method.Invoke($null, $parameters)
}
function Assert-Equal($actual, $expected, $name) {
    if ($actual -ne $expected) { throw "FAIL $name : expected '$expected', got '$actual'" }
    Write-Host "PASS $name"
}
Assert-Equal (Invoke-Helper $engine 'IsExtractorChallengeFailure' @('ERROR: challenge solving failed')) $true 'JS challenge classified'
Assert-Equal (Invoke-Helper $engine 'IsExtractorChallengeFailure' @('ERROR: a.addOnDisposeCallback is not a function')) $true 'Known JS player mismatch classified'
Assert-Equal (Invoke-Helper $engine 'IsExtractorChallengeFailure' @('HTTP Error 403')) $false '403 is not an extractor mismatch'
Assert-Equal (Invoke-Helper $engine 'IsAccessFailure' @('Sign in to confirm your identity')) $true 'Auth error never auto-heals'
Assert-Equal (Invoke-Helper $engine 'IsAccessFailure' @('HTTP Error 404: Not Found')) $true 'Deleted source not auto-retried'
Assert-Equal (Invoke-Helper $engine 'GetDownloadFailureMessage' @('ERROR: Requested format is not available', 1)) 'Selected quality is unavailable. Choose another quality and retry.' 'Unavailable quality is actionable'
$log = 'https://example.com/video?token=PRIVATE&sig=HIDDEN' + [Environment]::NewLine + 'Authorization: Bearer SECRET'
$redacted = Invoke-Helper $engine 'SanitizeVideoDiagnostic' @($log)
if ($redacted -match 'PRIVATE|HIDDEN|Bearer SECRET') { throw 'FAIL sensitive strings leaked' }
Write-Host 'PASS confidential URL and header data redacted'
$asset = New-Object 'System.Collections.Generic.Dictionary[string,object]'
$asset.Add('name','yt-dlp.exe')
$asset.Add('digest',('sha256:' + ('a' * 64)))
$assets = New-Object System.Collections.ArrayList
[void]$assets.Add($asset)
$release = New-Object 'System.Collections.Generic.Dictionary[string,object]'
$release.Add('assets', $assets)
$digestMethod = $updater.GetMethod('GetOfficialReleaseDigest', $flags)
$digestArgs = New-Object 'System.Object[]' 1
$digestArgs[0] = $release.PSObject.BaseObject
Assert-Equal ($digestMethod.Invoke($null, $digestArgs)) ('a' * 64) 'Official SHA-256 digest parsed'
Write-Host 'PASS runtime logic tests; no network or user files accessed'
