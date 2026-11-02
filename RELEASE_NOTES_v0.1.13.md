# FastDownloader Pro v0.1.13 — Windows Dashboard Startup Hotfix

This patch addresses cases in which FastDownloader appears to run in the Windows tray but opens its interface in a normal browser tab instead of a dedicated desktop app window.

## Changes
- Launch the dashboard in Chromium app mode with an explicitly requested standalone window.
- Keep the dashboard-only browser profile in a per-user writable directory (`%LOCALAPPDATA%\FastDownloader\DashboardProfile`), not beside the installed executable.
- Stop clearing Chromium lock files on each dashboard launch, which could interfere with an existing app window.
- Avoid treating ordinary Microsoft Edge tabs as existing FastDownloader app windows.

## Data safety
This patch does not intentionally remove downloads, download history or app settings. The dashboard-only Chromium profile is separate from FastDownloader's download data.

## Validation
The GitHub Windows build and automated regression tests must pass before publishing. A live desktop startup test is still needed to confirm the window behavior on affected Windows installations.
