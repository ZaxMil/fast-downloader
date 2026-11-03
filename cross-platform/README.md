# FastDownloader Pro — Cross-platform preview

**Status: experimental, not a replacement for the stable Windows application.**

The original Windows app uses .NET Framework/Windows Forms. This new, separate implementation runs a local Node.js download engine and a responsive browser dashboard, wrapped in an Electron desktop window for Windows, macOS and Linux.

## Available now
- Direct HTTP/HTTPS downloads with pause/resume (when the remote server supports HTTP Range), progress, history and persistent task state.
- yt-dlp media downloads using a SHA-256-verified binary bundled in desktop preview builds. For direct Node.js use, yt-dlp can be installed on PATH. Supported sites and formats depend on yt-dlp and any needed FFmpeg.
- Download file naming, 480p/720p/1080p selection for yt-dlp, safe task removal without deleting completed files.
- Responsive local interface and desktop shell for Windows, macOS and Linux.
- Local-only API on 127.0.0.1, same-origin POST validation and no cloud service.

## Not yet supported
- Windows browser extension/native messaging, torrents, aria2 segmented downloading, scheduled queues, self-update and all Windows-specific integrations.
- Native Android or iOS applications. The responsive dashboard is **not** a mobile downloader; it controls the computer on which the local engine runs. iOS background downloads require a separate native implementation.
- Code signing / notarization for desktop installers.

## Developer run
Requires Node.js 22 or later.

    cd cross-platform
    node server.js

Open the printed local URL. Direct downloads work without other tools. For video URLs in the CLI preview, install yt-dlp and optionally FFmpeg first. Desktop packages include verified yt-dlp; FFmpeg may still be needed to merge separate video/audio streams.

To run as a desktop app:

    npm ci
    npm run desktop

## Packaging
The desktop packaging script fetches the official pinned yt-dlp binary and verifies its SHA-256 checksum before packaging. GitHub Actions workflow "Cross-platform preview" tests Windows, macOS and Linux and builds unsigned preview artifacts. These are **not** automatically published as official releases.

## Data safety
The cross-platform app keeps state under the OS user data folder, and downloads in ~/Downloads/FastDownloader. It does not touch the existing Windows app's download history or installation. Removing a task does not delete the downloaded file.

## Security
The server listens on loopback only. Do not expose its port to a public network or place it behind an unauthenticated proxy. No cloud account or remote access is enabled.
