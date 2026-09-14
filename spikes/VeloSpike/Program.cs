using Microsoft.Win32;
using Velopack;

// S6: install / update / uninstall behaviour of Velopack for a per-user app.
const string RunValue = "MinkQuickLaxVeloSpike";
var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VeloSpikeLog");
Directory.CreateDirectory(logDir);
void Log(string s) => File.AppendAllText(Path.Combine(logDir, "log.txt"), $"[{DateTime.Now:HH:mm:ss.fff}] pid={Environment.ProcessId} {s}{Environment.NewLine}");

VelopackApp.Build()
    .OnAfterInstallFastCallback(v => Log($"hook after-install {v}"))
    .OnAfterUpdateFastCallback(v => Log($"hook after-update {v}"))
    .OnBeforeUninstallFastCallback(v =>
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        run?.DeleteValue(RunValue, throwOnMissingValue: false);
        Log($"hook before-uninstall {v}: removed Run value");
    })
    .OnFirstRun(v => Log($"first run {v}"))
    .Run();

var mgr = new UpdateManager(args.Length > 1 ? args[1] : Path.Combine(logDir, "none"), new UpdateOptions { ExplicitChannel = args.Length > 2 ? args[2] : null });
Log($"start args=[{string.Join(' ', args)}] version={mgr.CurrentVersion} installed={mgr.IsInstalled} exe={Environment.ProcessPath}");

if (mgr.IsInstalled)
{
    using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
    var value = $"\"{Environment.ProcessPath}\" --startup";
    if (run.GetValue(RunValue) as string != value)
    {
        run.SetValue(RunValue, value);
        Log($"Run value set: {value}");
    }
}

if (args.Length > 0 && args[0] == "update")
{
    var info = await mgr.CheckForUpdatesAsync();
    Log($"check: {(info is null ? "no update" : $"found {info.TargetFullRelease.Version} channel-downgrade={info.IsDowngrade}")}");
    if (info is not null)
    {
        await mgr.DownloadUpdatesAsync(info);
        Log("downloaded; applying and restarting");
        mgr.ApplyUpdatesAndRestart(info, ["after-update"]);
    }
}
return 0;
