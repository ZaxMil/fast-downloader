# FastDownloader Mobile

Android-first native app in Kotlin + Jetpack Compose, independent from the desktop Electron preview.

- Android source: [android/](android/)
- Seal research and implementation gates: [research/SEAL_ANALYSIS.md](research/SEAL_ANALYSIS.md)
- Current milestone: **foundation only** — launch UI, paste link, Android share target, HTTP/HTTPS URL validation.
- **Not yet a functional downloader**: transfer engine, metadata extraction, yt-dlp, background service, history and notifications are pending.
- No APK release or user-facing install claim until CI builds and real-device checks pass.
- Existing Windows/macOS/Linux desktop builds are unaffected.

To build, use Android Studio with JDK 17 and Android SDK 35; run Gradle \`assembleDebug\`.
