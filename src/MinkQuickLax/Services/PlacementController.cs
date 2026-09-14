using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Shell;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Surfaces;

namespace MinkQuickLax.Services;

/// <summary>Keeps one <see cref="IconWindow"/> per placed link in sync with the config, the monitors and the look.</summary>
public sealed partial class PlacementController : IDisposable
{
    private static readonly TimeSpan DisplaySettleDelay = TimeSpan.FromMilliseconds(500);

    private readonly ConfigStore _store;
    private readonly IMonitorProvider _monitorProvider;
    private readonly SystemEvents _systemEvents;
    private readonly TopmostKeeper _topmost;
    private readonly IconCache _icons;
    private readonly ThemeService _theme;
    private readonly SurfaceHost _surfaces;
    private readonly ProximityAnimator _animator;
    private readonly WindowMover _mover = new();
    private bool _slideNextMoves;
    private readonly ILogger<PlacementController> _logger;
    private readonly Dictionary<string, IconWindow> _windows = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _displayDebounce;
    private readonly DispatcherTimer _tooltipTimer;
    private IReadOnlyList<MonitorInfo> _monitors = [];
    private IconWindow? _hovered;

    public PlacementController(
        ConfigStore store,
        IMonitorProvider monitorProvider,
        SystemEvents systemEvents,
        TopmostKeeper topmost,
        IconCache icons,
        ThemeService theme,
        SurfaceHost surfaces,
        Platform.Input.MouseProximityTracker tracker,
        ILogger<PlacementController> logger)
    {
        _store = store;
        _monitorProvider = monitorProvider;
        _systemEvents = systemEvents;
        _topmost = topmost;
        _icons = icons;
        _theme = theme;
        _surfaces = surfaces;
        _logger = logger;
        _animator = new ProximityAnimator(tracker, () => _windows.Values);
        _displayDebounce = new DispatcherTimer { Interval = DisplaySettleDelay };
        _displayDebounce.Tick += (_, _) =>
        {
            _displayDebounce.Stop();
            RepositionAll();
        };
        _tooltipTimer = new DispatcherTimer();
        _tooltipTimer.Tick += (_, _) => ShowTooltipForHovered();
    }

    /// <summary>A link icon was clicked.</summary>
    public event Action<IconWindow, Link>? LaunchRequested;

    /// <summary>A link icon was right-clicked.</summary>
    public event Action<IconWindow, Link, Placement>? MenuRequested;

    /// <summary>Raised after hiding or showing everything.</summary>
    public event Action<bool>? HiddenChanged;

    public bool IsHidden { get; private set; }

    public void Start()
    {
        _monitors = _monitorProvider.GetMonitors();
        LogMonitors();
        _store.Changed += OnConfigChanged;
        _systemEvents.DisplaysChanged += OnDisplaysChanged;
        _theme.Changed += OnLookChanged;
        Sync(_store.Current);
    }

    /// <summary>A window was created for a placement (the arrange controller hooks its mouse input).</summary>
    public event Action<IconWindow>? WindowCreated;

    public event Action<IconWindow>? WindowRemoved;

    public IReadOnlyCollection<IconWindow> Windows => _windows.Values;

    public IReadOnlyList<MonitorInfo> Monitors => _monitors;

    /// <summary>Edit mode: icons wiggle, labels and tooltips are hidden.</summary>
    public bool IsArranging { get; private set; }

    public IconWindow? WindowFor(string placementId) => _windows.GetValueOrDefault(placementId);

    public void SetArranging(bool arranging)
    {
        IsArranging = arranging;
        _tooltipTimer.Stop();
        _surfaces.HideTooltip();
        var i = 0;
        foreach (var window in OrderedWindows())
        {
            // Spread the starting points so the icons do not wiggle in step.
            window.SetArranging(arranging, (i++ * 0.37) % 1, _theme.Current.ReduceMotion);
            window.SetSelected(false);
        }
    }

    /// <summary>A click that should open the link (decided by the arrange controller).</summary>
    public void RequestLaunch(IconWindow window) => WithLink(window, (link, _) => LaunchRequested?.Invoke(window, link));

    public void ToggleHidden() => SetHidden(!IsHidden);

    public void SetHidden(bool hidden)
    {
        if (hidden == IsHidden)
        {
            return;
        }
        IsHidden = hidden;
        _surfaces.HideTooltip();
        _surfaces.ClosePopup();
        var i = 0;
        foreach (var window in OrderedWindows())
        {
            window.AnimateVisibility(!hidden, Styles.Glass.Motion.HideShowStagger * i++, _theme.Current.ReduceMotion);
        }
        _topmost.Enabled = !hidden;
        HiddenChanged?.Invoke(hidden);
    }

    /// <summary>"Tidy up": every placement in a grid from the primary monitor's top-right corner, in the order added.</summary>
    public void Tidy()
    {
        var config = _store.Current;
        var primary = PositionMapper.Primary(_monitors);
        var area = PositionMapper.PlacementArea(primary, config.Settings.AllowOverTaskbar);
        var cell = PositionMapper.WindowSize(config.Settings.IconSize, primary);
        var slots = TidyLayout.Slots(config.Placements.Count, cell, area, primary.ToPixels(config.Settings.GridSize));
        _slideNextMoves = !_theme.Current.ReduceMotion;
        try
        {
            _store.Update(c => c with
            {
                Placements = [.. c.Placements.Select((p, index) => PositionMapper.WithCenter(p, slots[index], primary, c.Settings.AllowOverTaskbar))],
            });
        }
        finally
        {
            _slideNextMoves = false;
        }
    }

    /// <summary>A free spot next to an existing placement, for "place another copy".</summary>
    public Placement PlacementNextTo(Placement original)
    {
        var settings = _store.Current.Settings;
        var position = PositionMapper.ToScreen(original, settings.IconSize, _monitors, settings.AllowOverTaskbar);
        var monitor = position.Monitor;
        var area = PositionMapper.PlacementArea(monitor, settings.AllowOverTaskbar);
        var occupied = _windows.Values.Select(w => w.SquareRect).ToList();
        var desired = position.Center.Offset(position.Rect.Width, 0);
        var center = CollisionResolver.FindFreeCenter(desired, position.Rect.Size, occupied, area, monitor.ToPixels(settings.GridSize));
        return PositionMapper.WithCenter(new Placement { Type = PlacementType.Link, RefId = original.RefId }, center, monitor, settings.AllowOverTaskbar);
    }

    public void Dispose()
    {
        _store.Changed -= OnConfigChanged;
        _systemEvents.DisplaysChanged -= OnDisplaysChanged;
        _theme.Changed -= OnLookChanged;
        _displayDebounce.Stop();
        _tooltipTimer.Stop();
        _animator.Dispose();
        foreach (var window in _windows.Values)
        {
            _topmost.Unregister(window.Handle);
            window.Close();
        }
        _windows.Clear();
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e) => Sync(e.Current, e.Previous);

    private void OnDisplaysChanged()
    {
        _displayDebounce.Stop();
        _displayDebounce.Start();
    }

    private void OnLookChanged(Look look) => Sync(_store.Current, forceAppearance: true);

    private void Sync(AppConfig config, AppConfig? previous = null, bool forceAppearance = false)
    {
        var settings = config.Settings;
        var settingsChanged = forceAppearance || previous is null || !Equals(previous.Settings, settings);
        _animator.IdleOpacity = settings.IdleOpacity;
        _animator.RadiusDip = settings.ProximityRadius;
        _animator.Magnify = settings.Magnify;
        _animator.ReduceMotion = _theme.Current.ReduceMotion;

        var links = config.Links.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var wanted = config.Placements.Where(p => p.Type == PlacementType.Link && links.ContainsKey(p.RefId)).ToList();
        var wantedIds = wanted.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var gone in _windows.Keys.Where(id => !wantedIds.Contains(id)).ToList())
        {
            var window = _windows[gone];
            _windows.Remove(gone);
            _topmost.Unregister(window.Handle);
            if (ReferenceEquals(_hovered, window))
            {
                _hovered = null;
                _surfaces.HideTooltip();
            }
            WindowRemoved?.Invoke(window);
            window.Close();
        }

        var previousLinks = previous?.Links.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var previousPlacements = previous?.Placements.ToDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var placement in wanted)
        {
            var link = links[placement.RefId];
            var isNew = !_windows.TryGetValue(placement.Id, out var window);
            if (isNew)
            {
                window = CreateWindow(placement.Id);
            }
            var linkChanged = isNew || previousLinks is null || !previousLinks.TryGetValue(link.Id, out var oldLink) || !Equals(oldLink, link);
            if (isNew || linkChanged || settingsChanged)
            {
                window!.SetAppearance(settings.IconSize, settings.ShowLabels, link.Name, _theme.Current.Dark);
                if (IsArranging)
                {
                    window.SetArranging(true, 0, _theme.Current.ReduceMotion);
                }
            }
            if (isNew || linkChanged)
            {
                _ = LoadImageAsync(window!, link);
                _ = CheckTargetAsync(window!, link);
            }
            var moved = isNew || settingsChanged || previousPlacements is null || !previousPlacements.TryGetValue(placement.Id, out var oldPlacement) || !Equals(oldPlacement, placement);
            if (moved)
            {
                Position(window!, placement, settings);
            }
            if (isNew)
            {
                window!.CurrentOpacity = window.TargetOpacity = settings.IdleOpacity;
                window.CurrentScale = window.TargetScale = 1;
                window.SetProximity(settings.IdleOpacity, 1);
                if (!IsHidden)
                {
                    window.Show();
                }
                _topmost.Register(window.Handle);
            }
        }
        _animator.Refresh();
    }

    private IconWindow CreateWindow(string placementId)
    {
        var window = new IconWindow(placementId);
        _windows[placementId] = window;
        // Creates the HWND so styles are applied before the first Show.
        new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        window.MenuRequested += w => WithLink(w, (link, placement) => MenuRequested?.Invoke(w, link, placement));
        window.HoverChanged += OnHoverChanged;
        WindowCreated?.Invoke(window);
        return window;
    }

    private void WithLink(IconWindow window, Action<Link, Placement> action)
    {
        var config = _store.Current;
        if (config.FindPlacement(window.PlacementId) is { } placement && config.FindLink(placement.RefId) is { } link)
        {
            _surfaces.HideTooltip();
            _tooltipTimer.Stop();
            action(link, placement);
        }
    }

    private void Position(IconWindow window, Placement placement, AppSettings settings)
    {
        if (_monitors.Count == 0)
        {
            return;
        }
        var position = PositionMapper.ToScreen(placement, settings.IconSize, _monitors, settings.AllowOverTaskbar);
        if (_slideNextMoves && window.IsVisible && window.SquareRect.Width == position.Rect.Width)
        {
            _mover.Animate(window, position.Rect, position.Monitor.Dpi, Styles.Glass.Motion.Tidy);
        }
        else
        {
            _mover.Cancel(window);
            window.MoveTo(position.Rect, position.Monitor.Dpi);
        }
        if (position.IsFallback)
        {
            LogFallback(_logger, placement.Id, placement.Monitor);
        }
    }

    private void RepositionAll()
    {
        var monitors = _monitorProvider.GetMonitors();
        if (!monitors.SequenceEqual(_monitors))
        {
            _monitors = monitors;
            LogMonitors();
        }
        var config = _store.Current;
        foreach (var placement in config.Placements)
        {
            if (_windows.TryGetValue(placement.Id, out var window))
            {
                Position(window, placement, config.Settings);
            }
        }
        _topmost.RaiseAll();
        _animator.Refresh();
    }

    private async Task LoadImageAsync(IconWindow window, Link link)
    {
        var image = await _icons.GetAsync(link);
        window.SetImage(image ?? IconCache.LetterImage(link.Name));
    }

    private static async Task CheckTargetAsync(IconWindow window, Link link)
    {
        if (!Launcher.HasCheckableTarget(link))
        {
            window.SetMissing(false);
            return;
        }
        var missing = await Task.Run(() => Launcher.IsTargetMissing(link));
        window.SetMissing(missing);
    }

    private void OnHoverChanged(IconWindow window, bool hovering)
    {
        _tooltipTimer.Stop();
        if (hovering)
        {
            _hovered = window;
            var settings = _store.Current.Settings;
            if (settings.ShowLabels || IsArranging)
            {
                return;
            }
            _tooltipTimer.Interval = TimeSpan.FromMilliseconds(settings.TooltipDelayMs);
            _tooltipTimer.Start();
            // A drive may have come back (or gone away) since the last check.
            if (_store.Current.FindPlacement(window.PlacementId) is { } placement && _store.Current.FindLink(placement.RefId) is { } link)
            {
                _ = CheckTargetAsync(window, link);
            }
        }
        else if (ReferenceEquals(_hovered, window))
        {
            _hovered = null;
            _surfaces.HideTooltip();
        }
    }

    private void ShowTooltipForHovered()
    {
        _tooltipTimer.Stop();
        if (_hovered is { IsVisible: true } window && !_surfaces.HasOpenPopup && _store.Current.FindPlacement(window.PlacementId) is { } placement
            && _store.Current.FindLink(placement.RefId) is { } link)
        {
            _surfaces.ShowTooltip(link.Name, window.IconRect);
        }
    }

    private IEnumerable<IconWindow> OrderedWindows() =>
        _store.Current.Placements.Select(p => _windows.GetValueOrDefault(p.Id)).OfType<IconWindow>();

    private void LogMonitors()
    {
        foreach (var m in _monitors)
        {
            LogMonitor(_logger, m.Id, m.EdidKey, m.Bounds, m.WorkArea, m.Dpi, m.IsPrimary);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Monitor {Id} edid={Edid} bounds={Bounds} work={Work} dpi={Dpi} primary={Primary}")]
    private static partial void LogMonitor(ILogger logger, string id, string? edid, PixelRect bounds, PixelRect work, int dpi, bool primary);

    [LoggerMessage(Level = LogLevel.Information, Message = "Placement {Id} shown on the primary monitor; its monitor {Monitor} is not connected")]
    private static partial void LogFallback(ILogger logger, string id, string monitor);
}
