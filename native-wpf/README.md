# FastDownloader — Native Windows Prototype

This is an independent WPF proof of concept, not an update to the installed app.

## Why it exists

The original FastDownloader backend is a .NET application running on Windows. Its dashboard is currently rendered in a separate browser. This prototype tests a truly native Windows UI using WPF without WebView2, Chromium, Tauri, Electron, or browser tabs.

## What works in this prototype

- Connects to the already-running real FastDownloader engine at localhost:6801.
- Shows real, not fabricated, download counts, speeds, names, statuses and progress.
- Live refreshes, filtering, search, pause/resume (single item or all).
- New Download modal with supported URL input, best available video or audio and queue for later.
- Dynamic dark/light design, adaptive layout and native Win32 caption replacement.

## What this does not promise yet

- The old engine still starts a browser when it starts up. This preview does not start a browser itself and does not replace the engine. During this stage, start the installed app normally and leave its engine running; you may close the old browser tab afterwards.
- The original application's update, tray popup, browser extension capture and detailed file-path choice stay in the existing engine. Do not uninstall it.
- The experimental app is not the default system shortcut. No installer or registry modifications.
- No benchmark has yet proven superior idle CPU/memory. Measure WPF startup and idle side-by-side before promotion.
- The new transfer form uses the engine's configured Downloads folder; it does not pretend to change a per-download folder when the API does not support that.

## Running

1. Extract the Windows preview artifact into its own folder.
2. Keep FastDownloader engine running.
3. Open FastDownloader.NativePreview.exe.
4. Toggle Dark / Light from the sidebar.

The binary is self-contained and targets Windows x64. Close the preview window to exit the preview; it does not stop active downloads.

## Engineering path

1. Independent native UI with real engine communication.
2. Add an opt-in no-browser mode to the installed engine and test startup/fan impact, never altering default until proven.
3. Gradually migrate notification/tray, download dialogs and update UX while maintaining the existing engine.
4. Only then consider switching the primary launcher to the native UI.

Nothing in this prototype requires moving the download engine to Rust.
