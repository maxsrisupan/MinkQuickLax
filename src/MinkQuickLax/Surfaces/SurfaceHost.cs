using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Services;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// Owns the shared tooltip and the one open menu or notice. Our windows never get focus, so a click
/// anywhere outside the open surface (seen through Raw Input) closes it.
/// </summary>
public sealed class SurfaceHost : IDisposable
{
    private readonly ThemeService _theme;
    private readonly IMonitorProvider _monitors;
    private readonly MouseProximityTracker _tracker;
    private TooltipWindow? _tooltip;
    private GlassSurfaceWindow? _popup;

    public SurfaceHost(ThemeService theme, IMonitorProvider monitors, MouseProximityTracker tracker)
    {
        _theme = theme;
        _monitors = monitors;
        _tracker = tracker;
        _tracker.ButtonDown += OnButtonDown;
    }

    public bool HasOpenPopup => _popup is { IsVisible: true };

    public void ShowTooltip(string text, PixelRect iconRect)
    {
        var monitor = PositionMapper.MonitorAt(iconRect.Center, _monitors.GetMonitors());
        _tooltip ??= new TooltipWindow(_theme);
        _tooltip.ShowFor(text, iconRect, monitor.WorkArea, monitor.Scale);
    }

    public void HideTooltip() => _tooltip?.Hide();

    public void ShowMenu(PixelPoint at, string? header, IReadOnlyList<MenuEntry> entries)
    {
        HideTooltip();
        var area = PositionMapper.MonitorAt(at, _monitors.GetMonitors()).WorkArea;
        Open(new GlassMenuWindow(_theme, header, entries), size => GlassSurfaceWindow.PlaceAtPointer(at, size, area));
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

    public void ClosePopup()
    {
        _popup?.Close();
        _popup = null;
    }

    public void Dispose()
    {
        _tracker.ButtonDown -= OnButtonDown;
        ClosePopup();
        _tooltip?.Close();
    }

    private void Open(GlassSurfaceWindow window, Func<PixelSize, PixelPoint> place)
    {
        ClosePopup();
        _popup = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_popup, window))
            {
                _popup = null;
            }
        };
        window.ShowPlaced(place, animate: true);
    }

    private void OnButtonDown(PixelPoint at, MouseButton button)
    {
        if (_popup is { IsVisible: true } popup && !popup.Bounds.Contains(at))
        {
            ClosePopup();
        }
    }
}
