# FastDownloader Pro v0.1.28 — Standalone Desktop Window Fix

**Fix:** The v0.1.27 desktop dashboard unintentionally opened as a normal tab in the default browser. v0.1.28 restores a **separate app-style desktop window**, without the browser tab strip or address bar.

- Uses the existing Windows default browser when it supports standalone Chromium app mode (Brave, Chrome, or Edge).
- Falls back to a supported installed browser if the system default is incompatible.
- Does not create/delete browser profiles, interfere with browser tabs, or enable remote browser debugging.
- Reopening FastDownloader focuses an existing standalone dashboard window when available.
- Retains the compact v0.1.27 visual design, existing downloads, browser extension, updater, and restart/rollback safeguards.

**Note:** The window is independent of normal browser tabs but continues using an installed browser's rendering engine. A fully browser-independent embedded native shell is a separate architectural change.

To update, pause active downloads and use FastDownloader's in-app **Check Updates** → **Install & Restart App Now**. Close the old dashboard tab after the new window appears.
