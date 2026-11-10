# Integration test: run the updater without -SkipRestart using a slow test backend.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$version = [regex]::Match((Get-Content 'src/Program.cs' -Raw), 'CurrentAppVersion = "([^"]+)"').Groups[1].Value
if (-not $version) { throw 'Missing version.' }
$work = Join-Path $env:RUNNER_TEMP ('FastDownloader-Restart-Rehearsal-' + [guid]::NewGuid().ToString('N'))
$installedDir = Join-Path $work 'installed'
$stagedDir = Join-Path $work 'payload'
New-Item -Path $installedDir, $stagedDir -ItemType Directory -Force | Out-Null
$targetExe = Join-Path $installedDir 'FastDownloader.exe'
$newExe = Join-Path $stagedDir 'FastDownloader.exe'
$sourceFile = Join-Path $work 'FakeFastDownloader.cs'
$statusFile = Join-Path $work 'last-update.json'
$helper = (Resolve-Path 'scripts/apply-update.ps1').Path
$extensionZip = (Resolve-Path 'fast-downloader-extension.zip').Path
$uiZip = (Resolve-Path 'fast-downloader-ui.zip').Path
$csc = Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:SystemRoot 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
if (-not (Test-Path $csc)) { throw 'C# compiler unavailable.' }

$testHost = @'
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
namespace FastDownloader {
    public class UpdateManager {
        public const string CurrentAppVersion = "__VERSION__";
    }
    public class UpdaterRehearsalHost {
        public static void Main() {
            Thread.Sleep(3500);
            var listener = new TcpListener(IPAddress.Loopback, 6801);
            listener.Start();
            while (true) {
                using (TcpClient client = listener.AcceptTcpClient()) {
                    client.ReceiveTimeout = 4000;
                    using (NetworkStream network = client.GetStream()) {
                        using (var reader = new StreamReader(network, Encoding.ASCII, false, 4096, true)) {
                            string line;
                            while ((line = reader.ReadLine()) != null && line.Length > 0) { }
                        }
                        string body = "{\"app\":{\"currentVersion\":\"__VERSION__\",\"processId\":" + Process.GetCurrentProcess().Id + "}}";
                        byte[] json = Encoding.UTF8.GetBytes(body);
                        string header = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + json.Length + "\r\nConnection: close\r\n\r\n";
                        byte[] prefix = Encoding.ASCII.GetBytes(header);
                        network.Write(prefix, 0, prefix.Length);
                        network.Write(json, 0, json.Length);
                    }
                }
            }
        }
    }
}
'@.Replace('__VERSION__', $version)
[IO.File]::WriteAllText($sourceFile, $testHost, (New-Object Text.UTF8Encoding($false)))
$resource = (Resolve-Path 'ui/index.html').Path
& $csc /nologo /target:exe "/out:$newExe" "/resource:$resource,FastDownloader.TestUi" $sourceFile
if ($LASTEXITCODE -ne 0) { throw 'Test host compiler failed.' }
if ((Get-Item $newExe).Length -lt 50000) { throw 'Test executable too small.' }
'OLD_VERSION_BINARY' | Set-Content -Path $targetExe

try {
    $args = @(
        '-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass',
        '-File',$helper,
        '-TargetPid','999999',
        '-TargetExe',$targetExe,
        '-NewExe',$newExe,
        '-ExtensionZip',$extensionZip,
        '-UiZip',$uiZip,
        '-ExpectedVersion',$version,
        '-StatusFile',$statusFile,
        '-RestartTimeoutSeconds','28'
    )
    & powershell.exe @args
    if ($LASTEXITCODE -ne 0) {
        $report = if (Test-Path $statusFile) { Get-Content $statusFile -Raw } else { '<none>' }
        $log = if (Test-Path (Join-Path $work 'update.log')) { (Get-Content (Join-Path $work 'update.log') -Tail 20) -join [Environment]::NewLine } else { '<none>' }
        throw ("Live updater restart rehearsal failed: " + $report + [Environment]::NewLine + $log)
    }
    $record = Get-Content $statusFile -Raw | ConvertFrom-Json
    if ($record.status -ne 'success') { throw "Helper did not verify restart: $($record.message)" }
    $api = Invoke-RestMethod -Uri 'http://127.0.0.1:6801/api/update/status' -TimeoutSec 4
    if ($api.app.currentVersion -ne $version) { throw 'New running app reported wrong version.' }
    $ownerPid = [int]$api.app.processId
    $proc = Get-Process -Id $ownerPid -ErrorAction Stop
    if ($proc.HasExited -or [IO.Path]::GetFullPath($proc.Path) -ne [IO.Path]::GetFullPath($targetExe)) {
        throw 'API process is not the expected installed executable.'
    }
    if ((Get-FileHash $targetExe).Hash -ne (Get-FileHash $newExe).Hash) {
        throw 'Verified app executable does not match staged payload.'
    }
    $manifest = Get-Content (Join-Path $installedDir 'extension/manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.version -ne $version) { throw 'Verified extension version mismatch.' }
    if (-not (Test-Path (Join-Path $installedDir 'ui/index.html'))) { throw 'UI package missing after restart.' }
    Write-Host "PASS: Verified update to $version and live PID $ownerPid after delayed startup."
}
finally {
    # Only kill our test host, never a production FastDownloader instance.
    Get-CimInstance Win32_Process | Where-Object {
        $_.Name -eq 'FastDownloader.exe' -and
        [string]::Equals($_.ExecutablePath, $targetExe, [StringComparison]::OrdinalIgnoreCase)
    } | ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
}
