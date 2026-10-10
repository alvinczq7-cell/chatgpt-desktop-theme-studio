# Implementation status

## Implemented

- WinUI 3 theme editor for Chat, Work, and Codex profiles, with sync, per-background fit and position controls, local theme library, package v1 import/export, and sample packages.
- Image/GIF/MP4/WebM background media, simulated preview, and playback pause policies for battery, fullscreen, and Windows client-area animation preferences.
- Fail-closed host adapter interface and loopback-only CDP metadata probe.
- Loopback probe reports renderer page-target counts while discarding all target URLs and titles.
- English and Simplified Chinese UI, docs, GitHub Actions CI/release workflows, and an Inno Setup installer definition.
- Self-contained publishing with the WinUI control resource dictionary included; WebView2 profile data is stored in LocalAppData.

## Verified on the development machine

- Windows 11 x64, .NET SDK 10.0.401, and Windows App SDK 1.8.
- Release build completed with zero warnings/errors; 13 automated tests and static verification passed.
- Self-contained `win-x64` executable launched and remained active for 10 seconds.
- Inno Setup 6.7.3 generated an installer; the per-user install, Start Menu shortcut, and installed executable launch were verified.
- Portable ZIP, installer, sample packages, and SHA-256 checksums exist locally under ignored `artifacts/`.
- GitHub Actions Windows CI passed with Node 24 compatible checkout and .NET setup actions. The `v0.1.2` Windows Release workflow passed and published the setup, portable ZIP, four sample themes (including an animated GIF theme), and checksums.
- GitHub Release page: [`v0.1.2`](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/releases/tag/v0.1.2). Release assets show GitHub SHA-256 digests.
- The registered ChatGPT package activation API launched an isolated instance with a temporary profile and returned loopback CDP metadata. `/json/list` returned zero page targets; no DOM or CSS compatibility was verified.

## Not verified or enabled

- No ChatGPT desktop client build has passed end-to-end connection, render, use, and recovery checks. The adapter stays disabled; Safe launch is unavailable, and no styles are injected.
- Dynamic backgrounds and pause policies have not had full runtime QA across battery, fullscreen, and reduced-motion scenarios.
- The release has only been smoke-tested on the development machine, not on a clean Windows installation. The installer is unsigned.
