# FastDownloader Pro v0.1.14 — Reliable Starts & Live Transfers

- Recover genuinely stalled first-byte aria2 downloads automatically, with conservative connection settings and a bounded single-connection fallback. Queued and intentionally paused tasks are left untouched.
- Recover a stalled initial yt-dlp extraction once, while preserving partial files and protecting new processes from stale status updates produced by a paused/killed process.
- Show a real Windows tray flyout when the tray icon is clicked, listing active transfers with progress bars, transferred bytes, and current transfer speed.
- Add a persistent **Mute desktop alerts** button in the dashboard and a **Desktop Notifications** tray toggle. All tray balloons pass one duplicate-suppression and mute gate; completion popups respect the same preference.
- Report progress to one decimal using actual transferred bytes; show no misleading percentage when the total size is unknown.
- Serialize dashboard polling to prevent out-of-order snapshots and preserve progress-bar elements between updates for smooth, live width changes.
- Add automated regressions for percentage computation, notification preference flow, the tray UI, and stalled-start safeguards.

## Verification

Windows compilation and automated tests run in GitHub Actions. A real Windows download against the user's problematic URL and the tray flyout still need manual smoke-testing before relying on the repair for that particular source.
