# Third-Party Notices & Open Source Attributions

FastDownloader integrates with or utilizes the following third-party open-source projects:

---

### 1. aria2
- **Description**: Lightweight multi-protocol & multi-source command-line download utility.
- **License**: GNU General Public License v2.0 or later (GPL-2.0-or-later)
- **Copyright**: (C) 2006-2024 Tatsuhiro Tsujikawa and contributors.
- **Source Code**: [https://github.com/aria2/aria2](https://github.com/aria2/aria2)
- **Compliance**: FastDownloader executes `aria2c` as an independent external process via standard JSON-RPC over loopback socket. FastDownloader does not link against or modify aria2 source code.

---

### 2. yt-dlp
- **Description**: Feature-rich command-line video extraction and audio download utility.
- **License**: The Unlicense (Public Domain dedication)
- **Copyright**: Dedicated to the public domain under The Unlicense.
- **Source Code**: [https://github.com/yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp)
- **Compliance**: Executed as a standalone external CLI tool.

---

### 3. FFmpeg
- **Description**: Cross-platform solution to record, convert, and stream audio and video.
- **License**: GNU Lesser General Public License v2.1 or later (LGPL-2.1-or-later) / GNU General Public License v2.0 or later (GPL-2.0-or-later)
- **Copyright**: (C) 2000-2024 the FFmpeg developers.
- **Source Code**: [https://ffmpeg.org](https://ffmpeg.org)
- **Compliance**: Executed as a standalone subprocess by `yt-dlp` for media stream remuxing. FastDownloader does not bundle or modify FFmpeg code.

---

### 4. Microsoft Edge / WebView2 Runtime
- **Description**: Evergreen Chromium-based web browser platform utilized for desktop UI rendering in App Mode.
- **License**: Proprietary / Microsoft Software License Terms
- **Source / Documentation**: [https://learn.microsoft.com/en-us/microsoft-edge/](https://learn.microsoft.com/en-us/microsoft-edge/)
