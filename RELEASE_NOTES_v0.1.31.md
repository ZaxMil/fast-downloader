# FastDownloader Pro v0.1.31 — Update and restart reliability

## Fixed

- Updater now handles transient TCP listener records left by a terminated FastDownloader process during port 6801 handover. Only actual competing live listeners fail closed.
- Restart verification keeps enforcing the launched program's executable path, process identity, API response, and exact expected application version.
- During expected API interruption, the update window shows **Reconnecting to updated application...** rather than a misleading, frozen **Downloading FastDownloader.exe (0%)**.
- Added Windows regression cases for competing live listeners, stale dead-PID listener rows, failed-restart rollback, and verified in-place recovery. The real updater restart/companion-files test continues to run as well.

The video reliability improvements from v0.1.30 remain included. Existing downloads, user settings, browser extension, and compact desktop design are preserved.

## Important for users on v0.1.30 and earlier

Some older installations embed the original flawed restart helper. Therefore, the **first** in-app upgrade to v0.1.31 may still hit the old bug, despite the corrected update files being published. If the upgrade fails or rolls back, download the verified **FastDownloader-Setup.exe** from this release and install over your current version after closing the application, without uninstalling or deleting your download data. This is a one-time repair path; future updates use the corrected helper.

Do not interpret a successful network download as proof the app restarted correctly. Verify that the application displays v0.1.31 afterward. The prior v0.1.30 installer remains on the previous release page as a rollback option.
