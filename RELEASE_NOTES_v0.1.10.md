# FastDownloader Pro v0.1.10 — Accurate Downloads Disk Space

## Fixed

The **Downloads Disk** widget no longer displays a hard-coded `Free: 120 GB` value or a static 45% usage bar.

- The Windows application reads the **actual volume holding FastDownloader's downloads folder**. This may be `C:`, `D:`, or another local drive depending on where the app is installed.
- **Free space reflects the amount available to the current user**, not the size of the application's own download folder.
- The widget displays the drive letter, free GB, and actual disk usage percentage; hover to see full volume capacity and the downloads path.
- Disk usage is refreshed on startup and every 30 seconds without refreshing the whole interface.
- A nearly full disk gets a warning-colored usage bar.
- If Windows cannot read the drive, the widget shows **Free: unavailable** rather than guessing. Both English and Arabic labels are supported.

## Updating

Open **Updates → Check for Updates → Install & Restart** in FastDownloader Pro v0.1.9, or download the installer from the official GitHub Release. The release includes the Windows executable, installer, companion extension, and interface assets required by the built-in updater.

## Verification

Windows CI compiles the application, reads real disk capacity on its test runner, checks the disk widget's updates under simulated values, and validates the update packages. The exact space displayed on your PC will depend on the volume containing your installed FastDownloader's `downloads` folder.

This update does not change your downloads or remove files.
