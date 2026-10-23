# Chrome Web Store Listing — FastDownloader Integration

> Last Updated: 2026-10-21

---

## 1. Store Listing Information

**Extension Name:**
FastDownloader Integration

**Short Description (132 chars max):**
Accelerate file downloads and grab streaming media with 16 parallel connections using your local FastDownloader desktop application.

**Detailed Description (formatted for CWS dashboard):**
```text
FastDownloader Integration connects your browser directly to the high-performance FastDownloader desktop application on your PC, bringing multi-connection turbo downloading, in-page media grabbing, and seamless download automation to your daily workflow.

NOTE: This extension requires the FastDownloader desktop application installed and running on your Windows computer.

KEY FEATURES:

• 16x Multi-Connection Acceleration: Replaces slow single-thread browser downloads with parallel chunk downloading powered by Aria2.
• Automatic Download Interception: Seamlessly captures files (.zip, .exe, .iso, .mp4, .pdf, and more) as soon as you click them.
• Floating Video & Media Grabber: Automatically detects HTML5 video, audio, and streaming media on web pages with a sleek hover bar.
• One-Click Right-Click Tools:
  - "Fast Download Link (16x Turbo)" on any link
  - "Fast Download Video (Best Quality)" on video players
  - "Extract Audio (MP3)" directly from media
  - "Download Selected Links" to batch-grab multiple highlighted URLs
  - "Download All Links on Page" to scan and queue all archives and media files at once
• Authenticated Session Transfer: Preserves session cookies so downloads behind login screens download without 403 Forbidden errors.
• Canvas Stream Recording: Record WebGL, Canvas animations, and interactive games directly to high-quality video.

HOW TO USE:
1. Ensure FastDownloader is running on your PC (system tray icon active).
2. Browse any website — click any file download link to accelerate it automatically.
3. For streaming videos, hover over the player and click the FastDownloader floating pill, or right-click any link/media and choose "Fast Download".

PRIVACY FIRST:
FastDownloader Integration is strictly local. It communicates only with your computer on 127.0.0.1. No browsing history, cookies, or personal information are ever sent to remote servers or third parties.
```

**Category:**
Productivity

**Single Purpose:**
Connects the browser to the local FastDownloader desktop app to accelerate file downloads and capture streaming media.

**Primary Language:**
English

---

## 2. Permissions Justification (For CWS Review Form)

The Chrome Web Store review team inspects each permission. Copy and paste these exact justifications:

| Permission | Type | Justification for Reviewer |
| :--- | :--- | :--- |
| **`cookies`** | permissions | Required to forward session authentication cookies to the user's local download manager on `127.0.0.1`. This allows files requiring active login credentials to download successfully without terminating the user session. |
| **`downloads`** | permissions | Required to monitor and intercept standard browser download events (`chrome.downloads.onCreated`) and cancel them so they can be accelerated by the user's local multi-connection download engine. |
| **`contextMenus`** | permissions | Adds right-click context menu options ("Fast Download Link", "Download Video", "Extract Audio (MP3)", and "Download Selected Links") so users can trigger downloads on demand. |
| **`activeTab`** | permissions | Grants temporary access to the active tab when the user clicks the extension action or context menu to inspect media players and display feedback toasts. |
| **`storage`** | permissions | Saves user preferences, including enabling or disabling automatic browser download interception. |
| **`http://127.0.0.1:6801/*`** | host_permissions | Communicates with the local FastDownloader desktop application daemon running on loopback port 6801 to send download tasks and receive engine status. |
| **`<all_urls>`** | host_permissions | Allows the in-page media grabber content script to detect video players, streaming sources, and download links across arbitrary websites visited by the user. |

---

## 3. Privacy & Data Use Disclosure

Fill in the Chrome Web Store Developer Dashboard Privacy tab as follows:

### Data Collection
- **Does the extension collect user data?** Yes (Technically processes authentication info locally).
- **Personally identifiable info:** NO
- **Health / Financial info:** NO
- **Authentication info (Cookies):** YES
  - *Transmitted off-device?* **NO** (Localhost only)
  - *Purpose:* Core functionality (Passing session cookies to local download engine)
  - *Shared with third parties?* **NO**
- **Web history / User activity:** NO (Not stored or transmitted)
- **Website content:** NO (DOM media elements inspected locally in memory only)

### Data Use Certification
- [x] I certify that data is NOT sold to third parties.
- [x] I certify that data is NOT used for purposes unrelated to the extension's core functionality.
- [x] I certify that data is NOT used for creditworthiness or lending purposes.

---

## 4. Privacy Policy

**Live URL to Provide:**
Host `PRIVACY.md` on your GitHub repository (e.g. `https://raw.githubusercontent.com/.../PRIVACY.md` or as a GitHub Gist/Pages URL).

---

## 5. Graphics & Screenshots Requirements

Prepare before submitting:
1. **Store Icon:** 128×128 PNG (Already provided at `icons/icon128.png`)
2. **Screenshots (At least 1 required, 2-3 recommended):**
   - Size: **1280×800** or **640×400** pixels.
   - Screenshot 1: In-page floating video download button on a media player.
   - Screenshot 2: Right-click context menu showing "Fast Download Link (16x Turbo)" and "Extract Audio".
   - Screenshot 3: Extension popup showing "Connected (App Running)" and Auto-Interception toggle.
