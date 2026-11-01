# Security Policy — FastDownloader Pro

FastDownloader Pro handles download URLs and, when the optional browser companion is enabled, can interact with browser downloads and authentication cookies. Treat security reports involving these capabilities as sensitive.

## Reporting a vulnerability

**Do not post exploit details, private URLs, cookies, tokens or personal logs in public issues.**

If the GitHub repository's **Security → Report a vulnerability** option is available, use it to submit a private report. If the option is not available, open a minimal issue requesting a secure contact method **without** including vulnerability details.

A helpful private report includes the affected app/extension version, reproduction steps, impact, and any evidence that does not expose third-party credentials.

## Supported versions

The [latest published GitHub release](https://github.com/ZaxMil/fast-downloader/releases/latest) is the primary supported version for reporting and reproducing issues. No long-term support policy is promised for older versions.

## Permission boundaries

The browser companion currently declares broad page-access and cookie permissions in [extension/manifest.json](extension/manifest.json), and uses local native messaging. Review these permissions before installing it. `127.0.0.1` is a local loopback interface, but local communication alone is not a guarantee that every integration is immune to abuse.

Do not send sensitive logs or session details to a public issue. See [PRIVACY.md](PRIVACY.md), [DISCLAIMER.md](DISCLAIMER.md), and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
