using System.Windows.Threading;
using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Update;
using MinkQuickLax.Surfaces;

namespace MinkQuickLax.Services;

/// <summary>
/// When to look for updates and what to tell the user (SPEC 4.11): shortly after start and every 24 hours when automatic
/// checks are on, or when "Check for updates" is chosen. A downloaded version is announced next to the tray with
/// "Restart now"; otherwise it installs when the app closes.
/// </summary>
public sealed class UpdateController : IDisposable
{
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly UpdateService _updates;
    private readonly ConfigStore _store;
    private readonly SurfaceHost _surfaces;
    private readonly IMonitorProvider _monitors;
    private readonly Localizer _text;
    private readonly DispatcherTimer _timer = new() { Interval = FirstCheckDelay };
    private string? _announced;
    private bool _checking;

    public UpdateController(UpdateService updates, ConfigStore store, SurfaceHost surfaces, IMonitorProvider monitors, Localizer text)
    {
        _updates = updates;
        _store = store;
        _surfaces = surfaces;
        _monitors = monitors;
        _text = text;
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = CheckInterval;
            if (_store.Current.Settings.CheckForUpdates)
            {
                await CheckAsync(userAsked: false);
            }
        };
    }

    /// <summary>The downloaded version waiting to be installed, for the tray menu.</summary>
    public string? ReadyVersion => _updates.ReadyVersion;

    public void Start() => _timer.Start();

    /// <summary>"Check for updates" from the tray: always reports the result.</summary>
    public void CheckNow()
    {
        if (_updates.ReadyVersion is { } ready)
        {
            ShowReady(ready);
            return;
        }
        _ = CheckAsync(userAsked: true);
    }

    public void RestartNow()
    {
        _store.Flush();
        _updates.RestartNow();
    }

    /// <summary>Called while the app closes.</summary>
    public void InstallOnExit() => _updates.InstallAfterExit();

    public void Dispose()
    {
        _timer.Stop();
        _updates.Dispose();
    }

    private async Task CheckAsync(bool userAsked)
    {
        if (_checking)
        {
            return;
        }
        _checking = true;
        try
        {
            var result = await _updates.CheckAndDownloadAsync(_store.Current.Settings.UpdateChannel);
            switch (result.Outcome)
            {
                case UpdateOutcome.Ready when userAsked || result.Version != _announced:
                    ShowReady(result.Version!);
                    break;
                case UpdateOutcome.UpToDate when userAsked:
                    Notify(_text.Format("Update_UpToDate", result.Version ?? AppInfo.Version));
                    break;
                case UpdateOutcome.NotInstalled when userAsked:
                    Notify(_text["Update_NotInstalled"]);
                    break;
                case UpdateOutcome.Failed when userAsked:
                    Notify(_text["Update_Failed"]);
                    break;
            }
        }
        finally
        {
            _checking = false;
        }
    }

    private void ShowReady(string version)
    {
        _announced = version;
        _surfaces.ShowNotice(TrayCorner(), _text.Format("Update_Ready", version),
        [
            new NoticeButton(_text["Update_Later"]),
            new NoticeButton(_text["Update_RestartNow"], RestartNow, IsPrimary: true),
        ]);
    }

    private void Notify(string message) =>
        _surfaces.ShowNotice(TrayCorner(), message, [new NoticeButton(_text["Common_Close"], IsPrimary: true)]);

    /// <summary>The notification area sits at the bottom right of the main screen on a standard taskbar.</summary>
    private PixelRect TrayCorner()
    {
        var screen = _monitors.GetMonitors().FirstOrDefault(m => m.IsPrimary) ?? _monitors.GetMonitors()[0];
        var area = screen.WorkArea;
        return new PixelRect(area.Right - 1, area.Bottom - 1, area.Right, area.Bottom);
    }
}
