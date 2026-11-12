# Seal source study — FastDownloader Mobile

Source: https://github.com/JunkFood02/Seal
Source revision reviewed: 7677f61 (2026-11-03)
Source license: GPL-3.0; this project must not copy Seal code/assets without license compliance.

## Verified from source
- \`QuickDownloadActivity.kt\`: receives Android \`ACTION_VIEW\` and \`ACTION_SEND\`, extracts URL from shared text, then presents a Compose download configuration sheet.
- \`DownloadService.kt\`: a bound Android foreground service supplies a notification and a binder. Its comment explicitly states "This Service does nothing"; **do not infer that this class alone performs background downloads**.
- \`download/DownloaderV2.kt\`: separate download implementation (requires deeper behavior testing).
- \`ui/page/YtdlpUpdater.kt\` and \`settings/general/YtdlpUpdateDialog.kt\`: version checking, stable/nightly update controls.
- \`settings/network/NetworkPreferences.kt\`: configurable aria2c option.
- \`app/build.gradle.kts\`: Kotlin/Jetpack Compose, minSdk 24, targetSdk 35; yt-dlp Android bundle and OkHttp among dependencies.
- \`AndroidManifest.xml\`: internet, foreground-service declarations and share activity.

## FastDownloader Android architecture (independent implementation)
1. Kotlin + Jetpack Compose + Material 3 UI.
2. Share target for \`ACTION_SEND\` and \`ACTION_VIEW\`, validate HTTPS/HTTP links before processing.
3. Separate metadata probing and download orchestration. Use yt-dlp Android integration for supported media, and OkHttp for direct files.
4. Foreground service with Android 14+ appropriate foreground-service type, visible notifications, cancellation and resumable task persistence (Room).
5. MediaStore / Storage Access Framework for user-visible downloads, Unicode filenames, disk-space checks and conflict resolution.
6. Explicit Wi-Fi-only / mobile-data safeguards; quality and estimated size before download.
7. Engine updates only from authenticated release channels with checksum/signature verification; no arbitrary code injection.
8. Privacy: no account, telemetry or external relay by default; respect source authorization and DRM restrictions.

## Implementation gates
- Foundation: installable Android app shell, paste/share URL, local validation and navigation.
- Core: direct file transfers, pause/resume, integrity tests and storage APIs.
- Media: yt-dlp extraction, format selection, FFmpeg and supported-site tests.
- Release: real-device tests on Android 11 and current Android, permission/background tests, signed APK and reproducible CI.

## Limitations
This is a source review, not an REA APK behavioral capture. REA is installed on the server, but an authenticated APK and Android emulator/device are required for binary/runtime evidence. No Seal code or artwork has been copied.
