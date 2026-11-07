# FastDownloader Mobile third-party components

The Android APK includes the youtubedl-android library and its bundled third-party tools.
- youtubedl-android by yausername/JunkFood02 — GPL-3.0, https://github.com/yausername/youtubedl-android
- yt-dlp — Unlicense, https://github.com/yt-dlp/yt-dlp
- FFmpeg — subject to its configured build's LGPL/GPL license, https://ffmpeg.org/legal.html
- Kotlin / Jetpack Compose / AndroidX — respective Apache and other licenses.

The app source under `mobile/android` is offered under GPL-3.0 to comply with the
combined APK's GPL dependency. The separate desktop application is not relicensed.

Before production distribution, review the full transitive dependency list and bundled
native libraries/licenses. Alpha testers should use only permitted content.
