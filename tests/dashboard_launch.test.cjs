'use strict';
const fs = require('fs');
const assert = require('assert').strict;
const source = fs.readFileSync('src/Program.cs', 'utf8');

const start = source.indexOf('public static void LaunchDashboard(string appDir)');
const end = source.indexOf('private void CheckSchedulerTick()', start);
assert.ok(start >= 0 && end > start, 'The dashboard launcher must exist');
const launch = source.slice(start, end);

// Regression: v0.1.27 opened FastDownloader as a regular Brave browser tab.
assert.match(launch, /dashboardUrl = "http:\/\/127\.0\.0\.1:6801\//);
assert.match(launch, /FindChromiumAppHost\(\)/, 'Use a standalone app-window host');
assert.match(launch, /FileName = browser/, 'Launch the browser executable directly');
assert.match(launch, /--app=/, 'Chromium application mode hides tabs and address bar');
assert.match(launch, /--new-window/, 'Launch in a separate window');
assert.match(launch, /--window-size=1400,880/, 'Use a sensible desktop window size');
assert.match(launch, /UseShellExecute = false/, 'Do not ask Windows to open a URL as a tab');
assert.doesNotMatch(launch, /FileName = dashboardUrl/, 'Never use the URL file association');
assert.doesNotMatch(launch, /UseShellExecute = true/, 'No default-tab fallback');
assert.doesNotMatch(launch, /--user-data-dir=|--remote-debugging-port=|CleanProfileLocks/, 'Do not change the browser profile or enable debugging');
assert.match(launch, /WindowManager\.FocusExistingWindow\(\)/, 'Reopening should focus the existing app window');

// Respect the user's current default Chromium browser and provide safe fallback.
const finderStart = source.indexOf('private static bool IsSupportedDashboardBrowser(');
const finder = source.slice(finderStart, start);
assert.ok(finderStart >= 0);
assert.match(finder, /UrlAssociations\\https\\UserChoice/, 'Read Windows default HTTPS browser');
assert.match(finder, /Registry\.ClassesRoot\.OpenSubKey/, 'Resolve executable through the registered ProgID');
for (const binary of ['brave.exe', 'chrome.exe', 'msedge.exe'])
  assert.ok(finder.includes(binary), 'Supports ' + binary);
assert.match(finder, /File\.Exists\(candidate\)/, 'Only launch a real installed browser path');
assert.match(finder, /Path\.IsPathRooted\(candidate\)/, 'Reject relative executable paths');
assert.match(launch, /browser == null/, 'Fail visibly rather than silently opening another tab');

assert.match(source, /public void LaunchDefaultBrowser\(\)/, 'Preserve all tray hooks');
assert.match(source, /DownloaderAppContext\.LaunchDashboard\(appDir\)/, 'Keep duplicate-instance behavior');
console.log('PASS: standalone dashboard window and Brave/Chrome/Edge app-mode launch');
