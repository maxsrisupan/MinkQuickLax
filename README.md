# MinkQuickLax

[![CI](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml/badge.svg)](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml)

**English** · [ภาษาไทย](README.th.md)

A quick launcher for Windows. Each link is a small icon that floats above other windows, anywhere you place it. Click to open.

- Put single icons and groups (folders) anywhere on any monitor, and mix them freely
- Never steals focus from the app you are typing in, and stays out of the taskbar and Alt+Tab
- Scan installed apps, or add files, folders and web addresses, each web link with the browser of your choice
- Three styles: Glass, HUD and Dot Matrix, in light or dark
- Thai and English, with a built-in manual that works offline (Thai for now)
- Starts with Windows and updates itself
- Free and open source (MIT)

## Status

Beta. The first test release, [0.1.0-beta.1](https://github.com/maxsrisupan/MinkQuickLax/releases/tag/v0.1.0-beta.1), is out; a stable 0.1.0 follows after it has been used for a while.

## Install

1. Download `MinkQuickLax-win-Setup.exe` (or `MinkQuickLax-beta-Setup.exe` for a beta) from [Releases](https://github.com/maxsrisupan/MinkQuickLax/releases) and run it. It installs for your user only and needs no administrator rights.
2. The app is not code-signed yet, so Windows may show "Windows protected your PC". Choose **More info**, then **Run anyway**.

Uninstall from Windows Settings → Apps → Installed apps. Your links and settings stay in `%AppData%\MinkQuickLax`; use Settings → Data → "Delete all data and exit" first if you want them gone.

## Requirements

- Windows 10 22H2 or later, x64 (Windows 11 22H2 or later for blur effects)

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then:

```powershell
dotnet build MinkQuickLax.slnx
dotnet test --solution MinkQuickLax.slnx
dotnet run --project src/MinkQuickLax
```

The build also writes the manual (`Manual\th.html`, `Manual\en.html`) next to the app. The app shows an icon in the notification area; on Windows 11 it may be under "Show hidden icons".

To make an installer locally: `build/publish.ps1 -Version 0.1.0` then `build/pack.ps1 -Version 0.1.0` (output in `artifacts/releases`). Pushing a tag such as `v0.1.0` or `v0.1.0-beta.1` does the same on GitHub Actions and publishes the release.

## Documentation

The specification and plan are written in Thai:

- [docs/SPEC.md](docs/SPEC.md): requirements
- [docs/PLAN.md](docs/PLAN.md): architecture, milestones and progress
- [manual/th](manual/th): the user manual
- [CHANGELOG.md](CHANGELOG.md): changes in each version

## License

[MIT](LICENSE)
