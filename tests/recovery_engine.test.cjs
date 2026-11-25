"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const code = fs.readFileSync("src/Program.cs", "utf8");

// The exact regressions seen on the user's PC must never reappear.
assert.doesNotMatch(code, /Thread\.Sleep\(120000\)/, "No 2-minute engine startup blind spot");
assert.doesNotMatch(code, /Thread\.Sleep\(22000\)/, "No forced restart 22 seconds into extraction");
assert.doesNotMatch(code, /Arguments\s*=\s*"-U"/, "Never self-update the running yt-dlp executable");
assert.doesNotMatch(code, /File\.Delete\(destPath\)/, "Never blindly delete the installed yt-dlp.exe");
assert.match(code, /private readonly object ytDlpUpdateLock = new object\(\)/, "One serialized update pipeline");
assert.match(code, /GetOfficialReleaseDigest\(/, "Use official asset digest");
assert.match(code, /Sha256File\(downloading\), digest/, "Check file bytes against upstream SHA-256");
assert.match(code, /File\.Replace\(verifiedYtDlpStagedPath, target, backup, true\)/, "Atomic promotion with a backup");
assert.match(code, /if \(promoted\)/, "Do not roll back a previous backup when replacement fails");
assert.match(code, /HasLiveYtDlpProcess\(\)/, "Do not replace a binary while a task is running");
assert.match(code, /GetPreferredYtDlpExecutable\(\)/, "New tasks use verified staged engine");
assert.match(code, /Reused a previously verified staged engine/, "A failed installation doesn't redownload verified bytes");
assert.match(code, /Thread\.Sleep\(3000\)/, "Fast background engine health check");
assert.match(code, /IsExtractorChallengeFailure\(allErrors\)/, "Classify YouTube JS failures");
assert.match(code, /!IsAccessFailure\(allErrors\)/, "Never self-heal DRM or auth errors by randomly switching clients");
assert.match(code, /taskInfo\.StatusMessage = "Updating video extractor/, "Visible recovery phase");
assert.match(code, /taskInfo\.StatusMessage = "Retrying with verified updated extractor/, "Retry new executable, not stale binary");
assert.match(code, /bool autoQuality =/, "Explicit quality must not silently degrade");

assert.match(code, /ReadYtDlpVersionAtPath\(stage\)/, "Verify staged binary executes before promoting");
assert.match(code, /SanitizeVideoDiagnostic\(allErrors\)/, "No token/cookie leakage from video error logs");
assert.match(code, /p\.ErrorDataReceived \+=/, "Capture stderr while extraction runs");
assert.match(code, /Solving video source challenge/, "Progressive challenge message");
assert.match(code, /WatchdogTimedOutGeneration/, "Bounded watchdog");
assert.match(code, /DateTime\.UtcNow - taskInfo\.LastProgressUtc/, "Watchdog tracks actual byte progress");
assert.match(code, /taskInfo\.Status == "paused" \|\| taskInfo\.Status == "removed"/, "Cancel doesn't overwrite new task generation");
assert.match(code, /ResolveFfmpegLocation\(\)/, "Search bundled FFmpeg and valid PATH");
assert.match(code, /File\.Exists\(Path\.Combine\(ffmpegDir, "ffmpeg\.exe"\)\)/, "Do not pass a fictitious FFmpeg directory");
assert.match(code, /FFmpeg missing\. Install the video merger/, "Never claim HD merge works with no FFmpeg");
assert.match(code, /task\.StatusMessage :\s*\(task\.DownloadSpeed > 0/, "Native dialog displays the actual stage, not stale bytes");

// Check that source still has the same task resume and removal entrypoints.
for (const method of ["StartYtDlp(", "ResumeTask(", "PauseTask(", "RemoveTask("])
  assert.ok(code.includes(method), "Existing task operation preserved: " + method);

console.log("PASS: verified staged updater, video error classification, retry, FFmpeg, progress and watchdog guards");
