# FastDownloader Pro v0.1.11 — Reliable Download Table

The download table now displays **real task details** and respects column visibility, widths, and ordering.

## Fixed

- **File Name no longer collapses to a few letters:** minimum readable column width, proper filename truncation, full title on hover, and horizontal scrolling when needed.
- **Hide/Show works across all columns:** Date Added and Date Finished now obey their switches, along with Actions, Status and the other optional columns. File Name remains required so downloads can always be identified.
- **Column widths survive hiding:** resizing one column no longer overwrites hidden column widths with zero; saved undersized widths are clamped and reset uses column IDs rather than outdated positions.
- **Column layout remains usable:** table width is recalculated on resize, reorder, show/hide and window resize. A dedicated Columns control remains accessible even if Actions is hidden.
- **Dates are real task data:** enqueue time and completion time are recorded/persisted and included in the API for aria2 and yt-dlp downloads. Legacy tasks without verifiable dates show a dash instead of a guessed timestamp.
- **Live session and transfer numbers:** Session Downloaded sums increases in completed bytes instead of assuming a polling interval and adding instantaneous transfer rates.
- **Peak Speed** now labels the measured peak from the visible graph history rather than its minimum chart scale (formerly always showing 1 MB/s at idle).
- **Connection counts** reflect live aria2 task data instead of claiming 32 streams for every download.
- **Unknown file size** displays as unavailable, not a fabricated 0 B.
- **Engine versions** show live detected values or Unknown; they no longer fall back to obsolete hard-coded version dates.

## How to use

Open the new **Columns** icon beside the toolbar settings to show/hide columns. Drag a header divider to resize it, double-click the divider to auto-fit, or right-click a table header for full customization. Date Added and Date Finished are hidden by default on fresh configurations to keep the table compact; existing user preferences are preserved.

## Testing

Windows compilation, browser interception regressions, UI column and transfer-total tests, release package verification, and updater safety checks are run by GitHub Actions. Browser-rendering behavior should still be confirmed on a typical Windows installation.

This release does not delete files or download history.
