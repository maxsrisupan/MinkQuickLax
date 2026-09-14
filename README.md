# MinkQuickLax

[![CI](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml/badge.svg)](https://github.com/maxsrisupan/MinkQuickLax/actions/workflows/ci.yml)

**English** · [ภาษาไทย](README.th.md)

A quick launcher for Windows. Each link is a small icon that floats above other windows, anywhere you place it. Click to open.

- Put single icons and groups (folders) anywhere on any monitor, and mix them freely
- Never steals focus from the app you are typing in, and stays out of the taskbar and Alt+Tab
- Scan installed apps, or add files, folders and URLs
- Glass look that follows the Windows light/dark theme
- Thai and English
- Free and open source (MIT)

## Status

Early development. Nothing is ready to install yet. The project skeleton is in place; the next step is a set of technical spikes to prove the risky parts before building features.

## Requirements

- Windows 10 22H2 or later, x64 (Windows 11 22H2 or later for blur effects)

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then:

```powershell
dotnet build MinkQuickLax.slnx
dotnet test --solution MinkQuickLax.slnx
dotnet run --project src/MinkQuickLax
```

The app shows an icon in the notification area. On Windows 11 it may be under "Show hidden icons".

## Documentation

The specification and plan are written in Thai:

- [docs/SPEC.md](docs/SPEC.md): requirements
- [docs/PLAN.md](docs/PLAN.md): architecture, milestones and progress

## License

[MIT](LICENSE)
