# Contributing to FastDownloader Pro

Thanks for helping improve FastDownloader Pro. Bug reports, documentation fixes and focused pull requests are welcome.

## Before you start

- Search [existing issues](https://github.com/ZaxMil/fast-downloader/issues) to avoid duplicates.
- For bugs, include your Windows version, app version, clear reproduction steps and expected/actual results.
- For browser extension bugs, include the browser/version and whether the desktop application is running.
- Remove cookies, access tokens, signed URLs, personal filenames, download history and other private details from logs and screenshots.

## Working on the project

1. Fork the repository and create a small branch for one change.
2. Read the relevant source: `src/` for Windows application logic, `ui/` for the interface, `extension/` for browser integration.
3. Review build and installation scripts before running anything. This is a Windows application that integrates with external download engines.
4. Keep user-visible changes accessible in English and Arabic where possible.
5. Verify the part you changed. For UI changes, include a screenshot. For installer or extension changes, document what you tested and what you could not test.
6. Open a pull request explaining the problem, solution and verification steps.

## Scope and project safety

- Never include user cookies, downloaded private media, credentials, private server endpoints or complete authenticated URLs in commits or bug reports.
- Do not silently broaden browser extension permissions, change native-messaging trust boundaries, or disable safety checks.
- Use official engine download sources and preserve third-party licensing information in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
- Report security issues privately: see [SECURITY.md](SECURITY.md).

Contributors should not promise specific performance gains without reproducible measurements. Transfer performance depends on both client and server behavior.
