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
    private readonly Scanner.ScannerService _scanner;
    private readonly TrayController _tray;
    private readonly SingleInstance _instance;
    private readonly Settings.SettingsService _settings;
    private readonly StartupRegistration _startup;
    private readonly UpdateController _updates;
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
        Scanner.ScannerService scanner,
        TrayController tray,
        SingleInstance instance,
        Settings.SettingsService settings,
        StartupRegistration startup,
        UpdateController updates,
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
        _scanner = scanner;
        _arrange.AddRequested = () => _scanner.Show();
        _tray = tray;
        _instance = instance;
        _settings = settings;
        _startup = startup;
        _updates = updates;
        _logger = logger;
    }

    /// <summary>Settings and data were loaded before the windows were created (see <see cref="AppServices"/>).</summary>
    public LoadResult? LoadResult { get; set; }

    public void Start(bool launchedAtStartup)
    {
        LogStarting(_logger, launchedAtStartup, AppVersion);
        // Listen first, so a copy started while this one is still starting is not turned away; the command runs once
        // startup below has finished, because it is queued on the same dispatcher.
        _instance.CommandReceived += command => _application.Dispatcher.BeginInvoke(() => OnCommand(command));
        _instance.Listen();
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
        RegisterStartupOnFirstRun();
        // SPEC 4.12 / PLAN 3.1 step 6: nothing to show yet, so help the user pick apps (not when started at sign-in).
        if (!launchedAtStartup && _store.Current.Links.Count == 0)
        {
            _application.Dispatcher.BeginInvoke(() => _scanner.Show(firstRun: true));
        }
        _tray.ExitRequested += () => _application.Shutdown();

        if (LoadResult is { NeedsUserNotice: true } load)
        {
            _tray.ShowBalloon(load.Source == ConfigSource.Backup
                ? string.Format(_text.Culture, _text["Notice_ConfigRestored"], load.RestoredFrom?.CreatedAt.LocalDateTime.ToString("g", _text.Culture))
                : _text["Notice_ConfigReset"]);
        }

        // PLAN 3.1 step 7: look for updates in the background.
        _updates.Start();
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
        // SPEC 4.11: a downloaded version installs once this process has ended.
        _updates.InstallOnExit();
        _updates.Dispose();
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
        // SPEC 7: another copy was started, so show the icons and the settings window.
        _placements.SetHidden(false);
        _settings.Show();
    }

    /// <summary>
    /// SPEC 4.10: starting with Windows is on by default and registered the first time the app runs. Only installed
    /// copies do this, so a development build never adds itself to the Run key; a choice made in Task Manager is kept.
    /// </summary>
    private void RegisterStartupOnFirstRun()
    {
        if (LoadResult is not { IsFirstRun: true } || !AppInfo.IsInstalled || _startup.Read() != StartupState.Off)
        {
            return;
        }
        _startup.Enable(AppInfo.ExePath);
        LogStartupRegistered(_logger, AppInfo.ExePath);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered to start with Windows: {Path}")]
    private static partial void LogStartupRegistered(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting MinkQuickLax {Version} (at sign-in: {AtStartup})")]
    private static partial void LogStarting(ILogger logger, bool atStartup, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping")]
    private static partial void LogStopping(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command from another copy: {Command}")]
    private static partial void LogCommand(ILogger logger, string command);
}
