# FastDownloader Pro v0.1.8 — Download Reliability Update

This update addresses downloads that stalled until you manually paused and resumed them, and browser downloads that opened a confirmation window without actually starting.

## Fixes

- **No more silent browser handoff:** the extension now lets the original browser download continue unless FastDownloader confirms it accepted the task.
- **Unblocked browser downloads:** fixed Chrome's filename callback so a download does not remain stuck waiting for the extension.
- **More reliable first start:** added limited recovery when the aria2 engine's local API is still starting.
- **Clear failure feedback:** failed downloads are no longer reported as started; pending requests can be retried.
- **Direct media URLs:** direct video files are routed to the file-transfer engine instead of being sent to the media extractor solely because of their category.
- **Engine setup:** fixed an aria2 installation path typo affecting new installations.

## Updating

Open FastDownloader Pro and use the **Check for Updates / Update** control. Version **0.1.7** should detect **0.1.8** from GitHub Releases and download the updated desktop application, browser extension and interface.

Alternatively, download **FastDownloader-Setup.exe** from this release and install it over your existing version. The release includes the companion extension ZIP and UI ZIP for the in-app updater.

**Important:** Your installed app must have a working download engine for transfers to run. If an existing installation has missing/corrupt engine binaries, use the updated setup installer to restore the bundled aria2 and yt-dlp tools.

## Verification

Browser handoff regression tests and Windows compilation pass in CI. Installer packaging and bundle contents are checked by the Windows release workflow. Real-world download behavior may vary across providers, authentication requirements and server configurations; these fixes are not a claim of zero remaining bugs.

Download only material you're authorized to access. [Source and issue tracker](https://github.com/ZaxMil/fast-downloader).
