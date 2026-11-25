# Reliability recovery — Phase 1 (draft, not published)

## Proven failure case

AGY's local Windows evidence (user-supplied): FastDownloader v0.1.29 started with yt-dlp 2026.10.26; an immediate YouTube request failed in the 120-second blind window. Updating yt-dlp to 2026.11.23 resolved the observed JS extraction mismatch. The actual Windows installation and its private logs were not accessible from the connected Linux/VPS machine for this branch.

## Changes

- Background engine version check starts after ~3s (not 120s). No UI-thread or synchronous startup network request. The cached 30-minute official-release refresh limit remains.
- One serialized yt-dlp update pipeline. Official GitHub release asset SHA-256 digest must match downloaded bytes; a separately staged executable must pass a version smoke test before it is preferred. Staged files are reused across retries, promoted only when no yt-dlp processes are active, using File.Replace with a previous-version backup. No running self-update, no deletion of in-use executables.
- Status updates from stdout and stderr; concrete error classification; the known YouTube JS signature triggers at most one verified engine-refresh retry, then at most one compatible fallback for automatic-quality requests. Explicit quality is not silently downgraded; sign-in/403/404/DRM failures are not auto-retried.
- The 22-second forced extractor restart was removed. Advisory first wait at 30s; a generation-scoped no-progress watchdog detects true prolonged stalls and terminates safely. Unknown length is not itself a failure.
- Missing FFmpeg no longer receives a fictitious --ffmpeg-location. HD/MP3 requests display a real failure and an opt-in Repair button. Repair fetches official FFmpeg archive only after explicit consent (~196 MB), checks the official GitHub digest, extracts an isolated binary, verifies the executable, and promotes it safely.
- Transfer status displays the genuine backend stage even if a previous attempt downloaded partial bytes. Diagnostic logs redact query token/signature and authorization/cookie headers.

## Verified and pending

- Local static regression tests are executed through the existing Node test suite.
- Windows build invokes `scripts/test-recovery-runtime.ps1`, which loads the compiled native EXE and verifies known failure categories, failure messages, SHA digest parsing and secret redaction without network access.
- No package release or in-app deployment is authorized from this branch. An actual Windows PC acceptance run is **required** for the cold-launch YouTube reproduction, HD merge, two concurrent videos, real HTTP download, resume, cancel, network failure, and in-app updater/restart/rollback.
- No app data, cookies, browser profiles, installed files, or download folders were modified by this branch.

## Deferred by the provided plan

- Phase 0 backup and local executable/process inventory on the actual PC (unavailable from this connected Linux device).
- Phase 2 independent Windows process branding/WebView2 shell discussion.
- Phase 3 final compact Transfer dialog visual iteration after Phase 1 is accepted on real hardware.
