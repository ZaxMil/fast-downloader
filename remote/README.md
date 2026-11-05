# FastDownloader Pro — Private VPS Mode

A **separate, opt-in** Linux server component. It does not change the stable Windows download manager or the experimental local cross-platform desktop app.

## What it does

1. Open the private responsive dashboard from your phone or desktop.
2. Paste an HTTP/HTTPS link. The **VPS** downloads the file to its own disk; the file does not pass through your device.
3. View progress; pause, resume or remove a task without deleting its completed file.
4. If an [rclone](https://rclone.org/) remote is configured on the VPS, send the completed file to the remote's `FastDownloader/` folder. The VPS uploads it to cloud storage; your device only sends a small control request.

**Limits:** Direct file downloads use the Node.js HTTP engine (no segmented aria2 in this preview). Video links need yt-dlp on the VPS, and some providers may need FFmpeg, login or permissions. Downloading a file from the VPS **back to your device** will consume your device data. Uploading a local phone file to the VPS also consumes data. Files belong to the VPS and use VPS disk space.

## Server requirements

- Linux + Node.js 22+, optionally `yt-dlp` and `ffmpeg` for video links, `rclone` for cloud transfers.
- At least 5 GB available space; new downloads are limited to 20 GB each and 3 simultaneous transfers by default.
- A strong random access key set as `FASTDOWNLOADER_REMOTE_TOKEN` (at least 24 characters).
- The service intentionally listens on **127.0.0.1:6810** only. **Do not expose it directly on 0.0.0.0 or as an unauthenticated proxy.** Use a separately authenticated HTTPS reverse proxy/Cloudflare Tunnel and rate limits for remote access. Never put the access key in a URL, an issue, or a screenshot.

## Start privately

```sh
export FASTDOWNLOADER_REMOTE_TOKEN="$(openssl rand -hex 32)"
node remote/server.js
```

Open the loopback service only from the server or through a trusted SSH tunnel. The frontend asks for the private access key and stores it in the browser tab's session storage; it is sent only as an Authorization header.

For a persistent deployment, use a service manager and a **private root-readable environment file** rather than placing the key in command-line arguments or Git history. Back up the `~/fastdownloader-remote/state` task database as needed. Downloads are stored in `~/fastdownloader-remote/downloads` by default. These locations may be overridden with `FASTDOWNLOADER_REMOTE_STATE` and `FASTDOWNLOADER_REMOTE_DOWNLOADS`.

## Cloud uploads

The server intentionally **does not auto-connect a cloud account**. Configure a provider via rclone on the same Linux account that runs the service (e.g. Google Drive, SFTP, OneDrive, S3, WebDAV). `rclone listremotes` must show the authorized remote name. The dashboard then offers **Send to cloud** on completed files. The destination path is fixed to `<remote>:FastDownloader/<filename>` and arbitrary remote paths/commands are not accepted.

## Security boundaries

- Authentication is mandatory for every API route; access-key comparison uses constant-time matching.
- The server rejects private/reserved IP addresses and checks each HTTP redirect before downloading. **DNS rebinding and arbitrary video-site plugins warrant additional egress firewall protections before a broadly shared deployment.** Keep the VPS mode single-user and behind an authenticated gateway.
- Cross-origin browser requests are blocked, with no CORS allowance. Static files expose no private task data.
- No public listening socket, open upload endpoint, public share links, credentials in repository, or file deletion capability.
- Limit download sources to content you have permission to fetch. Before enabling public access, add per-user accounts/rate limits and review relevant privacy/security needs.

## Tests

```sh
node --test cross-platform/tests/*.test.js remote/tests/*.test.js
```

The tests check authentication, blocked SSRF targets, local fixture transfers, task safety, redirects, and the original cross-platform engine regressions.
