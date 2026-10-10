# ChatGPT Desktop Theme Studio

> An independent community project. Not affiliated with or endorsed by OpenAI.

A Windows companion app for creating and organizing local themes for the new ChatGPT desktop app. The planned target views are Chat, Work, and Codex.

## Current status

**The MVP manager is implemented and has passed a local Release build, six automated tests, and a short launch smoke check. The live ChatGPT adapter remains unverified and disabled.** The manager supports a local theme library, package import/export, simulated preview, and animated backgrounds; it does not yet claim to apply themes live to ChatGPT.

The target is Windows 11 x64 with .NET 10 and Windows App SDK. The repository includes Windows CI and release workflows for a portable ZIP and installer; those packages have not yet been built and verified locally. See the [progress checkpoint](docs/progress-2026-10-10.md) for the exact status.

## Planned features

- Separate appearance profiles for Chat, Work, and Codex.
- Local image, GIF, MP4, and WebM backgrounds.
- Per-surface color, opacity, blur, font, radius, and control-state settings.
- Silent looping with manual pause and power/accessibility pause rules.
- Local theme packages with media included; no scripts or arbitrary CSS.
- Safe launch, adapter status, disable, and restore controls.

## Build

Open `ThemeStudio.sln` with Visual Studio 2026 or a .NET 10 SDK installation that includes the Windows App SDK workload/components. The solution is unpackaged.

```powershell
dotnet build ThemeStudio.sln -c Release -p:Platform=x64
dotnet test tests/ThemeStudio.Tests/ThemeStudio.Tests.csproj -c Release -p:Platform=x64
```

The release workflow creates a portable folder ZIP, an Inno Setup installer, and SHA-256 checksums. The installer is currently unsigned. Live theme injection and safe launch remain disabled until the adapter passes version-specific validation.

## Theme package

Theme files are ZIP containers with `manifest.json`, `theme.json`, and an optional `assets/` directory. Only declared image/video media is accepted. Import validates paths and media extensions before extracting. The package format is versioned; see `docs/theme-package-v1.md`.

## Compatibility and safety

- Windows 11 x64 and the new ChatGPT desktop app are the initial targets.
- ChatGPT Classic, browser clients, Windows 10, ARM64, and macOS are out of scope for the first release.
- The live adapter is deliberately fail-closed: it will not inject styles until a version-specific compatibility mapping is tested and enabled.
- No conversation content is read or uploaded. The planned CDP connection is loopback-only; CDP is a privileged debugging interface, so it must not be exposed to the network.
- The companion does not modify files in the official app package.

## License

Application code is MIT licensed. Theme media remains subject to its author's license and must not be redistributed without permission.
