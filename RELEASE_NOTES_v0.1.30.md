# FastDownloader Pro v0.1.30 — Video reliability recovery

This is a focused reliability update, not a UI overhaul. It preserves existing user tasks, settings, browser integration, and the current compact desktop dashboard.

## Reliability fixes
- Start the background video-engine version check shortly after startup instead of waiting 120 seconds.
- Safely stage and verify upstream yt-dlp updates with official SHA-256 digest and executable version checks; do not modify the active binary while downloads are running. Keep backup and rollback logic.
- Detect common changes in YouTube extractor challenges and retry once with a verified engine; do not silently downgrade explicitly selected quality or bypass authorization errors.
- Remove the old hard-coded 22-second forced extractor restart. Improve no-progress detection and human-readable download error/status messages.
- Detect missing FFmpeg for HD/MP3 jobs and offer an explicit opt-in repair. The FFmpeg repair downloads roughly 196 MB from an official verified source, only with user approval.
- Keep useful progress and merging phases in the native transfer window; redact URL signatures and sensitive auth headers in error diagnostics.

## Known limits
- No universal guarantee for videos that need authentication, are unavailable, or have DRM restrictions.
- No native process branding or visual transfer-window redesign in this update; those are separate later improvements.
- Automated Windows build, updater/restart and regression tests have been run on a release candidate. Real-world acceptance on the user's Windows PC is still recommended after updating.

## Update instructions
Pause running downloads, then use Check Updates and Install & Restart App Now. The previously published v0.1.29 release remains available for rollback if necessary.
