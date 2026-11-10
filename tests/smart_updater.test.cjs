'use strict';
const fs = require('fs');
const vm = require('vm');
const assert = require('assert').strict;
const html = fs.readFileSync('ui/index.html','utf8').replace(/\r\n/g,'\n');
const cs = fs.readFileSync('src/Program.cs','utf8');
const ps = fs.readFileSync('scripts/apply-update.ps1','utf8');
const start = html.indexOf('    function showInstallStage(install) {');
const end = html.indexOf('    function updateHeaderUpdatePill(', start);
assert(start !== -1 && end > start);
const nodes = new Map();
const doc = {getElementById(id) {
  if (!nodes.has(id)) nodes.set(id, {
    textContent:'', title:'', disabled:false,
    style:{display:'none',width:'',color:''},
    classList:{toggle(name,on){this[name]=on}}
  });
  return nodes.get(id);
}};
let headerState = null;
const ctx = vm.createContext({document:doc, updateHeaderUpdatePill:(...args)=>{headerState=args;}, Number, Math});
vm.runInContext(html.slice(start,end), ctx);
const app = {currentVersion:'0.1.11',latestVersion:'0.1.12',updateAvailable:true,releaseNotes:'Reliability fixes'};
ctx.applyUpdateStatusToUI({app, installation:{status:'downloading',message:'Downloading UI.zip',progress:55},ytdlp:{currentVersion:'2026.10.26',latestVersion:'2026.10.26',updateAvailable:false},settings:{}});
assert.equal(nodes.get('updateProgressTrack').style.display,'block');
assert.equal(nodes.get('updateProgressFill').style.width,'55%');
assert.equal(nodes.get('btnApplyAppUpdate').disabled,true);
assert.equal(nodes.get('btnUpdateYtdlp').disabled,true);
assert.equal(nodes.get('btnUpdateYtdlp').textContent,'Up to date');
assert.equal(nodes.get('appUpdateBadge').textContent,'Update available');
ctx.applyUpdateStatusToUI({app,installation:{status:'failed',message:'Cannot close old process'},ytdlp:{currentVersion:'2026.10.26',latestVersion:'2026.11.01',updateAvailable:true},settings:{}});
assert.equal(nodes.get('btnUpdateFallback').style.display,'block');
assert.equal(nodes.get('btnApplyAppUpdate').disabled,false);
assert.equal(nodes.get('btnUpdateYtdlp').disabled,false);
assert.equal(nodes.get('btnUpdateYtdlp').classList['btn-primary'],true);
assert.match(nodes.get('updateInstallState').textContent,/Cannot close old process/);
ctx.applyUpdateStatusToUI({app:{currentVersion:'0.1.12',latestVersion:'0.1.12',updateAvailable:false},installation:{status:'success',message:'Installed v0.1.12'},ytdlp:{currentVersion:'2026.11.01',latestVersion:'2026.11.01',updateAvailable:false},settings:{}});
assert.equal(nodes.get('appUpdateBadge').textContent,'Up to date');
assert.equal(nodes.get('appUpdateDetails').style.display,'none');
assert.equal(nodes.get('btnUpdateYtdlp').disabled,true);
assert(cs.includes('DownloadAssetWithResume('),'Updater must resume downloads');
assert(cs.includes('VerifyReleaseHashes(tag, packages)'),'Updater must verify SHA256');
assert(cs.includes('QueueAppUpdate(out string message)'),'HTTP updater should not block for file transfers');

assert.match(cs, /CurrentAppVersion = "0\.1\.19"/, 'Version bump is required for installed app updates');
assert.match(cs, /this\.CachedYtDlpVersion = "Not checked";/, 'No yt-dlp binary executed on startup');
assert.match(cs, /this\.CachedAria2Version = "Not checked";/, 'No aria2 version process during startup');
assert.match(cs, /var res = UpdateYtDlp\(false\);/, 'Automatic engine updates must check upstream version first');
assert.match(cs, /public void RefreshInstalledEngineVersions\(\)/, 'Manual version refresh exists');
assert.match(cs, /updateManager\.RefreshInstalledEngineVersions\(\);/, 'Manual update check refreshes versions');
assert.match(cs, /string verifiedVersion = GetYtDlpVersion\(\)/, 'Version reused after update');
assert.doesNotMatch(cs, /this\.CachedYtDlpVersion = GetYtDlpVersion\(\);/, 'No synchronous Python unpack on constructor');

assert(cs.includes('dict["installation"] = ReadInstallStatus()'),'UI needs helper status after restart');
assert(ps.includes('Stop-Process -Id $TargetPid -Force'),'Updater may terminate only the verified hung parent');
assert(ps.includes('[IO.Path]::GetFullPath($actualPath)'),'Verify process path before force stopping');
assert.match(cs, /appInfo\["processId"\] = Process\.GetCurrentProcess\(\)\.Id/, 'API must expose process identity');
assert.match(ps, /Confirm-UpdatedApp \$started\.Id/, 'Helper must verify the newly launched process');
assert.match(ps, /\$reportedPid -eq \$newPid/, 'Restart must reject a stale API process');
assert.match(ps, /\$runningVersion -eq \$ExpectedVersion/, 'Restart must verify version and PID');
assert.match(ps, /Wait-BridgeRelease/, 'Update must check port ownership before replacement');
assert.match(ps, /Assert-UpdateBinaryVersion/, 'Executable version must be validated before installation');
assert.match(ps, /RestartTimeoutSeconds = 120/, 'Allow real first-start latency with finite timeout');
assert.match(ps, /Get-BridgeOwner/, 'Process-aware port diagnostics required');
assert.match(ps, /Restore-OldVersion/, 'Failed upgrade must preserve rollback');
assert.match(cs, /Active file downloads detected/, 'Active downloads must block shutdown');
assert.match(cs, /Active video download detected/, 'Video processing must block shutdown');
assert.match(html, /stage\.status === 'success' && installed === expected/, 'UI must not signal success before the helper verifies restart');
assert(!ps.includes('Get-Process FastDownloader | Stop-Process'),'Never terminate all processes by name');

assert.match(cs, /class WindowsSafeFileName/, 'Native dialog must normalize browser titles before saving');
assert.match(cs, /class NativeDialogTheme/, 'Native dialogs must honor dashboard theme');
assert.match(cs, /NativeDialogTheme.Read\(\)/, 'Download form reads shared theme');
assert.match(cs, /WindowsSafeFileName.Normalize\(initialName\)/, 'Pre-fill must contain Windows-safe filename');
assert.match(cs, /WindowsSafeFileName.Normalize\(original\)/, 'Edits must be normalized before transfer');
assert.match(cs, /Unsupported Windows characters/, 'Show inline filename advice');
assert.match(cs, /"light" \|\| value == "zaxcore"/, 'Allowed theme settings');
assert.match(cs, /urlPath == "\/api\/preferences\/theme" && method == "POST"/, 'Theme sync endpoint');
assert.doesNotMatch(cs.slice(cs.indexOf('public class DownloadConfirmDialog : Form'), cs.indexOf('public class UnifiedTaskInfo')), /MessageBox.Show\(this, "Enter a valid file name"/, 'No blocking system warning for video titles');
assert.match(html, /fetch\(API_BASE \+ '\/api\/preferences\/theme'/, 'Browser theme synced to native dialog');

console.log('PASS: smart updater state, progress, failure/fallback, yt-dlp button, resume/hash, scoped restart');
