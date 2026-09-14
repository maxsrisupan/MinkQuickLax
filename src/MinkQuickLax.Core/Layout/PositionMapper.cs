using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Layout;

/// <param name="IsFallback">The saved monitor is not connected; shown on the primary monitor without changing the saved position.</param>
public sealed record ScreenPosition(MonitorInfo Monitor, PixelRect Rect, bool IsFallback)
{
    public PixelPoint Center => Rect.Center;
}

/// <summary>Converts saved 0–1 positions to physical pixels and back (SPEC 4.4 position rules).</summary>
public static class PositionMapper
{
    /// <summary>Room around the icon for its shadow and magnification (SPEC 5.3).</summary>
    public const int WindowMarginDip = 12;

    public static PixelSize WindowSize(int iconSizeDip, MonitorInfo monitor) =>
        PixelSize.Square(monitor.ToPixels(iconSizeDip + 2 * WindowMarginDip));

    /// <summary>The area icons may occupy: the work area, or the whole monitor when placing over the taskbar is allowed.</summary>
    public static PixelRect PlacementArea(MonitorInfo monitor, bool allowOverTaskbar) =>
        allowOverTaskbar ? monitor.Bounds : monitor.WorkArea;

    /// <summary>Finds the saved monitor by device path, then by EDID; null when it is not connected.</summary>
    public static MonitorInfo? FindMonitor(string? id, string? edidKey, IReadOnlyList<MonitorInfo> monitors)
    {
        if (!string.IsNullOrEmpty(id))
        {
            var exact = monitors.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }
        if (!string.IsNullOrEmpty(edidKey))
        {
            var sameModel = monitors.Where(m => string.Equals(m.EdidKey, edidKey, StringComparison.OrdinalIgnoreCase)).ToList();
            // Two identical monitors without serials cannot be told apart; do not guess.
            if (sameModel.Count == 1)
            {
                return sameModel[0];
            }
        }
        return null;
    }

    public static MonitorInfo Primary(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is required.", nameof(monitors));
        }
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public static ScreenPosition ToScreen(Placement placement, int iconSizeDip, IReadOnlyList<MonitorInfo> monitors, bool allowOverTaskbar)
    {
        var monitor = FindMonitor(placement.Monitor, placement.MonitorEdid, monitors);
        // No monitor recorded at all (hand-written config): the primary monitor is simply where it belongs.
        var isFallback = monitor is null && !(string.IsNullOrEmpty(placement.Monitor) && string.IsNullOrEmpty(placement.MonitorEdid));
        monitor ??= Primary(monitors);

        var area = PlacementArea(monitor, allowOverTaskbar);
        var center = new PixelPoint(
            area.Left + (int)Math.Round(Clamp01(placement.X) * area.Width, MidpointRounding.AwayFromZero),
            area.Top + (int)Math.Round(Clamp01(placement.Y) * area.Height, MidpointRounding.AwayFromZero));
        var rect = PixelRect.FromCenter(center, WindowSize(iconSizeDip, monitor)).MoveInside(area);
        return new ScreenPosition(monitor, rect, isFallback);
    }

    /// <summary>Returns the placement moved to <paramref name="center"/> on <paramref name="monitor"/>.</summary>
    public static Placement WithCenter(Placement placement, PixelPoint center, MonitorInfo monitor, bool allowOverTaskbar)
    {
        var area = PlacementArea(monitor, allowOverTaskbar);
        return placement with
        {
            Monitor = monitor.Id,
            MonitorEdid = monitor.EdidKey,
            X = Fraction(center.X - area.Left, area.Width),
            Y = Fraction(center.Y - area.Top, area.Height),
        };
    }

    /// <summary>The monitor containing <paramref name="point"/>, or the nearest one.</summary>
    public static MonitorInfo MonitorAt(PixelPoint point, IReadOnlyList<MonitorInfo> monitors) =>
        monitors.FirstOrDefault(m => m.Bounds.Contains(point))
        ?? monitors.OrderBy(m => m.Bounds.DistanceTo(point)).First();

    private static double Fraction(int offset, int length) =>
        length <= 0 ? 0.5 : Math.Round(Clamp01(offset / (double)length), 5);

    private static double Clamp01(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5;
}
