"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.join(__dirname, "..");
const html = fs.readFileSync(path.join(root, "ui/index.html"), "utf8");
const backend = fs.readFileSync(path.join(root, "src/Program.cs"), "utf8");

// Inline dashboard JavaScript must stay executable after UI edits.
let scriptCount = 0;
for (const script of html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/gi)) {
  if (script[1].trim()) {
    new vm.Script(script[1], { filename: "dashboard-inline.js" });
    scriptCount++;
  }
}
assert.ok(scriptCount >= 2, "dashboard script tags parsed");

const start = html.indexOf("    function getTaskProgress(task) {");
const end = html.indexOf("    let tasks = [];", start);
assert.ok(start >= 0 && end > start, "live percent function is present");
const context = vm.createContext({ Number, Math });
vm.runInContext(html.slice(start, end), context);

assert.equal(context.getTaskProgress({ engine: "aria2", totalLength: 1000, completedLength: 123, status: "active" }), 12.3);
assert.equal(context.getTaskProgress({ engine: "aria2", totalLength: 0, completedLength: 500, progress: 72, status: "active" }), null, "unknown total must not show fake percent");
assert.equal(context.getTaskProgress({ engine: "yt-dlp", totalLength: 0, completedLength: 500, progressExact: 14.75, status: "downloading" }), 14.75);
assert.equal(context.getTaskProgress({ engine: "yt-dlp", totalLength: 500, completedLength: 115, progressExact: 10, status: "downloading" }), 23, "actual bytes outrank stale parser percent");
assert.equal(context.getTaskProgress({ engine: "aria2", totalLength: 500, completedLength: 500, status: "complete" }), 100);

assert.match(html, /let taskPollInFlight = false/);
assert.match(html, /if \(taskPollInFlight\) return/);
assert.match(html, /signal: controller\.signal/);
assert.match(html, /reconcileLiveRows\(tbody, html\)/);
assert.match(html, /oldFill\.style\.width = newFill\.style\.width/);
assert.match(html, /id="notificationToggleBtn" onclick="toggleNotifications\(\)"/);
assert.match(html, /body: JSON\.stringify\(\{ notificationsEnabled: !notificationsEnabled \}\)/);

assert.match(backend, /public bool NotificationsEnabled \{ get; set; \}/);
assert.match(backend, /dict\["notificationsEnabled"\] = NotificationsEnabled/);
assert.match(backend, /notificationsEnabled = engine\.NotificationsEnabled/);
assert.match(backend, /private void ShowNotification\(/);
assert.match(backend, /recentNotifications\.TryGetValue\(key, out last\)/);
assert.equal((backend.match(/trayIcon\.ShowBalloonTip\(/g) || []).length, 1, "all balloons must pass mute/dedupe wrapper");
assert.match(backend, /if \(!engine\.NotificationsEnabled\) return;/);
assert.match(backend, /class TrayDownloadsForm : Form/);
assert.match(backend, /public void ShowNearTray\(\)/);
// The updated tray uses a shared compact progress control and reuses rows rather
// than disposing and recreating every bar on every live poll.
assert.match(backend, /row\.Progress = new ModernProgressBar \{/);
assert.match(backend, /row\.Progress\.Value = task\.Total > 0 \? \(int\)Math\.Round\(pct\) : 0/);
assert.match(backend, /while \(rows\.Count < visibleCount\) CreateRow\(\)/);
assert.match(backend, /ClientSize = new Size\(358, newHeight\)/);
assert.match(backend, /AutoEllipsis = true/);
assert.doesNotMatch(backend, /16 parallel chunk segments/);
assert.match(backend, /SendAriaAction\("aria2\.forcePause", gid\)/);
assert.match(backend, /state\.CompletedLength > 0 \|\| state\.DownloadSpeed > 0/);
assert.match(backend, /"max-connection-per-server", "1"/);
assert.match(backend, /public int RunGeneration;/);
assert.match(backend, /MonitorNewYtDownload\(taskInfo\)/);
assert.match(backend, /generation != Interlocked\.CompareExchange\(ref taskInfo\.RunGeneration/);
assert.match(backend, /--continue --newline/);

console.log("PASS: real byte progress, sequential polling, tray transfers, global notification mute/deduplication and watchdog");
