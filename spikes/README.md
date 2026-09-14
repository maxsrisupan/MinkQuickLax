# M1 spikes

Throwaway experiments that proved the risky parts before M2. Results are recorded in `docs/PLAN.md` section 11 on `main`.

| Command (from `spikes/SpikeLab/bin/Release/net10.0-windows/win-x64`) | Spike |
|---|---|
| `SpikeLab.exe icons [noshadow] [swwin] [smooth] [only=s1\|s2\|s3]` | S1 icon windows, S2 drag loop, S3 raw input proximity |
| `SpikeLab.exe acrylic` | S4 blur on no-activate windows |
| `SpikeLab.exe scan` | S5 AppsFolder scan, 256px icons, launch |
| `SpikeLab.exe tray [pathguid]` | S7 tray icon, own menu, TaskbarCreated |
| `SpikeLab.exe webview` | S8 WebView2 local file with #id |
| `SpikeLab.exe displays` | S9 monitor ids (run again after reboot/replug to compare) |
| `spikes/VeloSpike` + `dotnet vpk pack` | S6 install / update / channel switch / uninstall |

Output goes to `spikes/results/` (git-ignored: it contains screenshots and the list of installed apps).
