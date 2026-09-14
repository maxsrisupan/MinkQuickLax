using System.Globalization;
using System.Windows;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Services;
using MinkQuickLax.Tray;

namespace MinkQuickLax;

/// <summary>Startup and shutdown order of the running app (PLAN.md 3.1).</summary>
public sealed partial class AppShell
{
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;
    private static readonly string AppVersion = typeof(AppShell).Assembly.GetName().Version?.ToString() ?? "?";

    private readonly Application _application;
    private readonly ConfigStore _store;
    private readonly ThemeService _theme;
    private readonly Localizer _text;
    private readonly SystemEvents _systemEvents;
    private readonly PlacementController _placements;
    private readonly ArrangeController _arrange;
    private readonly LinkActions _actions;
    private readonly TrayController _tray;
    private readonly SingleInstance _instance;
    private readonly ILogger<AppShell> _logger;

    public AppShell(
        Application application,
        ConfigStore store,
        ThemeService theme,
        Localizer text,
        SystemEvents systemEvents,
        PlacementController placements,
        ArrangeController arrange,
        LinkActions actions,
        TrayController tray,
        SingleInstance instance,
        ILogger<AppShell> logger)
    {
        _application = application;
        _store = store;
        _theme = theme;
        _text = text;
        _systemEvents = systemEvents;
        _placements = placements;
        _arrange = arrange;
        _actions = actions;
        _tray = tray;
        _instance = instance;
        _logger = logger;
    }

    /// <summary>Settings and data were loaded before the windows were created (see <see cref="AppServices"/>).</summary>
    public LoadResult? LoadResult { get; set; }

    public void Start(bool launchedAtStartup)
    {
        LogStarting(_logger, launchedAtStartup, AppVersion);
        ApplySettings(_store.Current.Settings);
        _store.Changed += (_, e) =>
        {
            if (!Equals(e.Previous.Settings, e.Current.Settings))
            {
                ApplySettings(e.Current.Settings);
            }
        };
        _systemEvents.SettingsChanged += () => _theme.Apply(_store.Current.Settings, SystemSettings.Read());

        _placements.Start();
        _tray.ExitRequested += () => _application.Shutdown();

        if (LoadResult is { NeedsUserNotice: true } load)
        {
            _tray.ShowBalloon(load.Source == ConfigSource.Backup
                ? string.Format(_text.Culture, _text["Notice_ConfigRestored"], load.RestoredFrom?.CreatedAt.LocalDateTime.ToString("g", _text.Culture))
                : _text["Notice_ConfigReset"]);
        }

        _instance.CommandReceived += command => _application.Dispatcher.BeginInvoke(() => OnCommand(command));
        _instance.Listen();
        GC.KeepAlive(_actions);
    }

    public void Stop()
    {
        LogStopping(_logger);
        _arrange.Done();
        _arrange.Dispose();
        _tray.Dispose();
        _placements.Dispose();
        _store.Flush();
    }

    private void ApplySettings(AppSettings settings)
    {
        _text.SetCulture(settings.Language switch
        {
            LanguageSetting.Th => CultureInfo.GetCultureInfo("th-TH"),
            LanguageSetting.En => CultureInfo.GetCultureInfo("en-US"),
            _ => SystemCulture,
        });
        _theme.Apply(settings, SystemSettings.Read());
    }

    private void OnCommand(string command)
    {
        LogCommand(_logger, command);
        // Another copy was started: make sure the icons are visible. The settings window opens here from M7.
        _placements.SetHidden(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting MinkQuickLax {Version} (at sign-in: {AtStartup})")]
    private static partial void LogStarting(ILogger logger, bool atStartup, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping")]
    private static partial void LogStopping(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command from another copy: {Command}")]
    private static partial void LogCommand(ILogger logger, string command);
}
