# Privacy Policy for FastDownloader Integration

**Last Updated:** October 21, 2026

FastDownloader Integration ("the Extension") is developed as a companion extension for the local FastDownloader desktop application. We are committed to protecting your privacy and ensuring you have complete transparency regarding how data is handled.

---

### 1. Zero Remote Data Collection

- **No Remote Servers:** The Extension does NOT collect, transmit, store, or sell any personal data, browsing history, or user information to any external or remote servers.
- **Local-Only Communication:** All communications are strictly restricted to your own local machine via loopback address (`http://127.0.0.1:6801`).

---

### 2. Permissions & Data Usage

The Extension requests only the permissions necessary to perform user-initiated download tasks:

| Permission | Purpose | Data Handling |
| :--- | :--- | :--- |
| **`cookies`** | Allows downloading files from websites where the user is logged in (session cookies). | Cookies for the specific download URL are forwarded only to the local FastDownloader app on your computer (`127.0.0.1`). They are never sent anywhere else. |
| **`downloads`** | Intercepts standard browser downloads to accelerate them with the multi-connection desktop engine. | File URLs and filenames are sent to your local desktop engine. |
| **`contextMenus`** | Provides right-click options ("Fast Download Link", "Download Video", "Extract Audio"). | Operates on the link or media element clicked by the user. |
| **`activeTab`** | Detects downloadable media streams on the tab the user is actively viewing. | Inspects DOM media elements on user demand. |
| **`storage`** | Saves user preferences (such as enabling/disabling automatic download interception). | Saved locally in your browser profile. |
| **`<all_urls>`** | Enables media stream detection and download interception across websites you choose to visit. | Processed entirely client-side inside your browser. |

---

### 3. Third-Party Sharing

We do not sell, trade, rent, or transfer any user data or credentials to third parties under any circumstances. No analytics, tracking pixels, or third-party advertising SDKs are included in the Extension.

---

### 4. Children's Privacy

The Extension does not knowingly collect or solicit any personal information from children under the age of 13.

---

### 5. Changes to This Policy

If changes are made to this Privacy Policy, the updated version will be posted here with an updated revision date.

---

### 6. Contact

If you have any questions about this Privacy Policy, please contact the developer via the official repository or support channel:
- **Project:** FastDownloader Pro
- **Local Application:** FastDownloader Desktop
