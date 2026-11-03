# FastDownloader Mobile

Android-first Kotlin + Jetpack Compose application, independent from the desktop builds.

- [Android source](android/)
- [Seal research](research/SEAL_ANALYSIS.md)
- **Current alpha:** paste/share links; enqueue direct HTTPS file downloads using Android DownloadManager; system-managed background transfer, retry and notifications; open Android Downloads and check last transfer status.
- **Not supported yet:** yt-dlp video-page extraction, quality selection, manual pause/resume, in-app download list and advanced filename handling.
- DownloadManager owns transfer persistence. It may rename duplicate destinations or reject some URLs; actual media page URLs generally require extraction first.
- No signed APK release or real-device verification yet. Existing desktop releases unaffected.

Build with JDK 17, Android SDK 35 and Gradle 8.11.1 (\`gradle assembleDebug\` or Android Studio).
