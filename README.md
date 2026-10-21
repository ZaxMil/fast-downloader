# ⚡ FastDownloader Pro

[![License: MIT](https://img.shields.io/badge/License-MIT-emerald.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com/windows)
[![Engine: aria2](https://img.shields.io/badge/Engine-aria2%2016x%20Turbo-cyan.svg)](https://github.com/aria2/aria2)
[![Architecture: Native Edge App](https://img.shields.io/badge/Architecture-Edge%20App%20Mode%20%28~30MB%20RAM%29-purple.svg)](https://learn.microsoft.com/en-us/microsoft-edge/)

An ultra-fast, modern download manager inspired by **Internet Download Manager (IDM)**, re-engineered for 2026 with an obsidian cyberpunk glassmorphic dashboard, **16-connection parallel chunk streaming** via `aria2c`, and full 4K online media extraction via `yt-dlp`.

---

## ✨ Features

- **⚡ 16-Connection Parallel Turbo**: Multi-chunk download splitting via `aria2c` accelerates transfers up to 10x over standard browser downloads.
- **⏱️ Advanced Queue Scheduler (IDM-Grade)**:
  - **Main Queue & Night Queue**: Categorize tasks and schedule off-peak downloads.
  - **Custom Time Windows**: Set start and stop times (e.g. run between 02:00 AM and 07:00 AM).
  - **Sequential Downloading**: Enforce max concurrent downloads so tasks finish sequentially at full line speed.
  - **Auto-Shutdown**: Automatically powers off or sleeps your PC when queued tasks finish.
- **📊 60 FPS Real-Time Canvas Waveform**: Live bandwidth monitor with 60-second speed history and dynamic transfer gauges.
- **🌐 Browser Extension Included**:
  - Chrome, Microsoft Edge, and Brave support via Manifest V3.
  - Injects floating `[ ⬇ Download Video ]` grab buttons over web video players (YouTube, Vimeo, etc.).
  - HTML5 Canvas & Blob media recorder.
- **🧲 BitTorrent & Magnet Support**: Native support for `.torrent` files and `magnet:?xt=` links with DHT and peer exchange.
- **🪶 Ultra-Lightweight Native App**: Runs via Windows Edge App Mode (~30 MB RAM, zero Electron overhead, starts instantly).
- **🔒 Single-Instance Guarantee**: Win32 Mutex and BroadcastChannel locks prevent duplicate background processes or dashboard windows.

---

## 🚀 Quick Start

### Option 1: Prebuilt Release
1. Download the latest release from the [Releases](https://github.com/ZaxMil/fast-downloader/releases) page.
2. Run `scripts/setup-binaries.ps1` to automatically fetch the official helper engines (`aria2c`, `yt-dlp`, `ffmpeg`).
3. Double-click `FastDownloader.exe`.

### Option 2: Build From Source (Zero External Tools Required)
Windows has the C# compiler pre-installed. You can build the executable in seconds:

```cmd
# 1. Clone the repository
git clone https://github.com/ZaxMil/fast-downloader.git
cd fast-downloader

# 2. Download the official helper engines (aria2, yt-dlp, ffmpeg)
powershell -ExecutionPolicy Bypass -File scripts\setup-binaries.ps1

# 3. Compile the executable
build.bat

# 4. Launch
FastDownloader.exe
```

---

## 🧩 Installing the Browser Extension

1. Open your browser's extension manager:
   - **Chrome**: `chrome://extensions`
   - **Edge**: `edge://extensions`
   - **Brave**: `brave://extensions`
2. Enable **Developer mode** (toggle in the upper right corner).
3. Click **Load unpacked**.
4. Select the `extension/` directory inside this repository.

---

## 📁 Repository Structure

```
fast-downloader/
├── src/
│   └── Program.cs             # C# backend daemon (HTTP bridge + Tray + Mutex + Scheduler)
├── ui/
│   ├── index.html             # Obsidian glassmorphic dashboard (Single-page app)
│   ├── favicon.ico            # Dashboard favicon
│   └── logo.png               # Brand logo
├── extension/                 # Manifest V3 browser companion extension
│   ├── manifest.json
│   ├── background.js
│   ├── content.js
│   ├── content.css
│   └── icons/
├── scripts/
│   └── setup-binaries.ps1     # Official upstream binary fetcher
├── .github/
│   └── workflows/build.yml    # Automated CI build workflow
├── build.bat                  # 1-click native compiler script
├── DISCLAIMER.md              # Acceptable use & legal notice
├── THIRD_PARTY_NOTICES.md     # Open source attributions (aria2, yt-dlp, FFmpeg)
└── LICENSE                    # MIT License
```

---

## ⚖️ Legal Disclaimer & Open Source Compliance

FastDownloader is an open-source utility designed for downloading content that users own or have explicit legal rights to download. FastDownloader does not include tools to circumvent digital rights management (DRM) or access paywalled media. Please review [DISCLAIMER.md](DISCLAIMER.md) before use.

FastDownloader utilizes third-party open-source components (`aria2`, `yt-dlp`, and `FFmpeg`) executed as external standalone processes. All third-party software retains its original licensing. Full attributions and source links are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
