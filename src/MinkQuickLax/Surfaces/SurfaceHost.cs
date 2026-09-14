using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Services;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// Owns the shared tooltip and the one open menu, notice or group panel. Our windows never get focus, so a click
/// anywhere outside the open surface (seen through Raw Input) or Esc closes it.
/// </summary>
public sealed class SurfaceHost : IDisposable
{
    private readonly ThemeService _theme;
    private readonly IMonitorProvider _monitors;
    private readonly MouseProximityTracker _tracker;
    private readonly EscapeKeyWatcher _escape;
    private TooltipWindow? _tooltip;
    private SurfaceWindow? _popup;
    private PixelRect _keepOpenArea;

    public SurfaceHost(ThemeService theme, IMonitorProvider monitors, MouseProximityTracker tracker, EscapeKeyWatcher escape)
    {
        _theme = theme;
        _monitors = monitors;
        _tracker = tracker;
        _escape = escape;
        _tracker.ButtonDown += OnButtonDown;
        _escape.Pressed += ClosePopup;
    }

    public bool HasOpenPopup => _popup is { IsVisible: true };

    public void ShowTooltip(TooltipContent content, PixelRect iconRect)
    {
        var monitor = PositionMapper.MonitorAt(iconRect.Center, _monitors.GetMonitors());
        _tooltip ??= new TooltipWindow(_theme);
        _tooltip.ShowFor(content, iconRect, monitor.WorkArea, monitor.Scale);
    }

    public void HideTooltip() => _tooltip?.Hide();

    public void ShowMenu(PixelPoint at, string? header, IReadOnlyList<MenuEntry> entries)
    {
        HideTooltip();
        var area = PositionMapper.MonitorAt(at, _monitors.GetMonitors()).WorkArea;
        Open(new MenuWindow(_theme, header, entries), size => SurfaceWindow.PlaceAtPointer(at, size, area));
    }

    /// <summary>Shows a notice beside <paramref name="anchor"/> (an icon, or the cursor for the tray).</summary>
    public void ShowNotice(PixelRect anchor, string message, IReadOnlyList<NoticeButton> buttons)
    {
        HideTooltip();
        var monitor = PositionMapper.MonitorAt(anchor.Center, _monitors.GetMonitors());
        var gap = monitor.ToPixels(8);
        Open(new NoticeWindow(_theme, message, buttons), size =>
        {
            // Prefer below the anchor, centered; flip above when there is no room.
            var x = anchor.Center.X - size.Width / 2;
            var y = anchor.Bottom + gap + size.Height <= monitor.WorkArea.Bottom ? anchor.Bottom + gap : anchor.Top - gap - size.Height;
            var rect = new PixelRect(x, y, x + size.Width, y + size.Height).MoveInside(monitor.WorkArea);
            return new PixelPoint(rect.Left, rect.Top);
        });
    }

    /// <summary>The open popup (menu, notice or group panel), if any.</summary>
    public SurfaceWindow? Popup => _popup is { IsVisible: true } popup ? popup : null;

    /// <summary>Opens a group panel beside its folder. Clicks inside the folder or panel do not close it.</summary>
    public void ShowPanel(GroupPanelWindow panel, PixelRect folder)
    {
        HideTooltip();
        var monitor = PositionMapper.MonitorAt(folder.Center, _monitors.GetMonitors());
        _keepOpenArea = folder;
        Open(panel, size =>
        {
            var topLeft = GroupPanelWindow.PlaceBeside(folder, size, monitor.WorkArea, monitor.ToPixels(8), out var origin);
            panel.SetUnfoldOrigin(origin);
            return topLeft;
        });
    }

    public void ClosePopup()
    {
        _popup?.Close();
        _popup = null;
        _escape.Stop();
    }

    public void Dispose()
    {
        _tracker.ButtonDown -= OnButtonDown;
        _escape.Pressed -= ClosePopup;
        ClosePopup();
        _tooltip?.Close();
    }

    private void Open(SurfaceWindow window, Func<PixelSize, PixelPoint> place)
    {
        ClosePopup();
        _popup = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_popup, window))
            {
                _popup = null;
                _escape.Stop();
            }
        };
        window.ShowPlaced(place, animate: true);
        _escape.Start();
    }

    private void OnButtonDown(PixelPoint at, MouseButton button)
    {
        if (_popup is { IsVisible: true } popup && !popup.Bounds.Contains(at) && !(popup is GroupPanelWindow && _keepOpenArea.Contains(at)))
        {
            ClosePopup();
        }
    }
}
