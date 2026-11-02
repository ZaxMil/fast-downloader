# FastDownloader Pro v0.1.12 — Smart Update Recovery

This release replaces the old one-shot application update workflow with a trackable and safer update process.

## Fixed

- **Restart stalls:** the updater waits for the old app to close, then may close **only its verified original PID** if it remains stuck. It never kills every process with the same name. It keeps a rollback backup and checks the launched app's version.
- **Actual update progress:** the app downloads files on a background worker and reports stages (checking, downloading, verifying, waiting, installing, restarting, success, or failure). The Updates dialog shows download progress, retries, and useful diagnostics.
- **Resume interrupted downloads:** temporary partial files can resume using HTTP byte ranges where the release server supports them, with bounded retries and a full SHA-256 verification against the official GitHub release manifest before installation.
- **Clear failure path:** a failed handoff shows an actionable error and offers the official installer. The updater writes its status to `%LOCALAPPDATA%\FastDownloader\last-update.json` and a log in the update directory.
- **yt-dlp update button:** is disabled when the installed engine is current or when upstream data has not yet been checked. A live upstream check establishes whether a newer release exists before the update action is enabled.
- **No fake engine versions:** unavailable local versions appear as Unknown rather than an outdated static number.
- **Check Engines** now checks availability without unconditionally downloading an engine.

## Updating from v0.1.11

**If the old update dialog hangs at "Restarting & Checking Version", exit FastDownloader from the system tray and run the official v0.1.12 setup installer once.** The old updater cannot receive this fix until the new version is installed.

After installing v0.1.12, future updates will use the new progress-aware updater. Existing download folders and downloaded files are not intentionally removed.

## Validation

GitHub Actions compiles the Windows application and bundles the app, extension and UI. It runs simulated update-state/engine-button regression tests, confirms updater resource embedding, verifies installer staging/rollback, and checks all release assets. Real-world restart behavior should still be tested on Windows with the program running from the system tray.
