# FastDownloader Android 0.2.0-alpha.1 — TEST APK ONLY

This is an **ARM64 Android 10+ alpha test APK**, not a stable/final release. Debug-signed; a future build may need uninstall before install because the debug signing key may differ.

## Implemented
- Native Kotlin/Jetpack Compose Android UI (not a website).
- HTTPS link sharing and paste; basic malformed/private URL screening.
- Direct HTTPS file downloads via Android DownloadManager.
- yt-dlp based video/audio extraction with best, 720p, 480p and MP3 options.
- Android foreground-service video downloads, status notifications, pause/attempted resume using partial files.
- Completed media copied to public Downloads/FastDownloader using MediaStore, without broad storage permission.

## Not yet verified / limitations
- **No physical Android device test** or emulator smoke test has passed.
- Site support and actual playback of output are not verified; logins, DRM and some sources will fail.
- Video pause/resume and background behavior are implemented but NOT yet verified on real devices.
- Single media job at a time; no playlists, cookies, batch queue, scheduler, format list or advanced history.
- HTTP (unencrypted) links are intentionally rejected. No distribution signing key or Play Store build.
- Minimum Android 10 (API 29), ARM64 only; Android 11 intended test target.

## Licensing
Android app module is distributed under GPL-3.0 due to the included youtubedl-android and FFmpeg components; Android source and third-party notices are in this public repository. Desktop modules retain their existing licenses.
