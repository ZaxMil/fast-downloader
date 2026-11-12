# FastDownloader Mobile — Android Alpha

Native Kotlin/Jetpack Compose Android application; independent of desktop.

**Status:** Development alpha, NOT a stable release. Android 10+ (API 29) and arm64-v8a.
See [Alpha release notes](ALPHA_RELEASE_NOTES.md) for scope and untested features.

## Features under development
- Paste/share secure HTTPS URLs.
- Direct file transfers with Android's DownloadManager.
- Embedded yt-dlp media downloads, selected best/720p/480p/MP3 formats.
- Foreground download notifications, pause and attempted continuation of .part files.
- Finished media exported through MediaStore to public Downloads/FastDownloader.
- No login, analytics or remote proxy.

## Testing
```sh
cd mobile/android
./gradlew :app:testDebugUnitTest :app:assembleDebug
```
Requires Java 17 and Android SDK platform 35. Build also runs in GitHub Actions.
The CI-built APK is a debug-signed **test APK**, not verified on a physical phone.
Real device install, download quality, background, storage, pause/resume and UI tests are outstanding.

## Licensing
The mobile application integrates GPL-3.0 components. See [mobile GPL license](LICENSE-GPL-3.0),
[third-party notices](THIRD_PARTY_NOTICES.md), and the public sources under `mobile/android`.
Unrelated desktop code remains under the root license.
