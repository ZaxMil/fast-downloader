# FastDownloader Pro v0.1.9 — Reliable Update & Restart

This release fixes the **"Downloading & Restarting..." window that never finishes**, even after the updated files are downloaded.

## What's fixed

- The updater no longer loops forever trying to overwrite a running executable.
- A separate, embedded update helper waits a bounded time for the old process to exit.
- Update files are fetched from **one specific release** and checked against official SHA-256 digests before installation.
- The existing application, browser extension, and UI are backed up before replacement.
- On failure, the updater attempts to restore the previous files, restart the old app and writes a diagnostic log.
- On success, the updater restarts the application and checks that its local API reports **v0.1.9** before reporting success.
- The Updates window only reports success after confirming the version; otherwise it offers the official installer.
- In-progress browser downloads are still protected by the fixes from v0.1.8.
- **Facebook video downloads:** use yt-dlp's native HTTP transfer to avoid stalls associated with nested aria2 transfers and retry failed network fragments.
- **Stalled direct downloads:** a one-time automatic recovery attempts to resume new aria2 downloads that remain active at zero bytes.
- **480p selection:** the media extractor now prefers available formats at or below 480p when selected.
- **Arabic filenames:** video metadata is decoded as UTF-8 to preserve Arabic titles; malformed replacement-character titles are not used as output names.
- **Facebook CDN segments:** fewer initial segments improve compatibility with short-lived source links.

## Important for users of v0.1.7 and v0.1.8

The **old in-app updater contains the restart bug**. If it is stuck, close FastDownloader from the system tray, then use **FastDownloader-Setup.exe from this release** to install v0.1.9. That manually replaces the buggy updater so future in-app updates can use the new helper.

Keep downloaded files in your normal Downloads folder; do not delete your download directory. Avoid updating while a large transfer is active if it cannot be resumed.

## Verification

The Windows CI builds all four release packages and runs browser interception regressions, checksum/build checks, and the updater's staging/version-rejection tests. Real-world restart behavior still needs confirmation on installed Windows devices.

When troubleshooting, inspect `%LOCALAPPDATA%\FastDownloader\update.log` and `last-update.json`. Never post cookies or private download URLs in public bug reports.
