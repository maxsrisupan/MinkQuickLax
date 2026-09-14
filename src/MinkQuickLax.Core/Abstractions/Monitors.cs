using MinkQuickLax.Core.Layout;

namespace MinkQuickLax.Core.Abstractions;

/// <summary>A connected monitor in physical pixels.</summary>
/// <param name="Id">Device path from QueryDisplayConfig; stable while the monitor stays on the same port.</param>
/// <param name="EdidKey">Manufacturer-product-serial from EDID; survives a port change. Null when unknown.</param>
/// <param name="Dpi">Effective DPI (96 = 100%).</param>
public sealed record MonitorInfo(string Id, string? EdidKey, PixelRect Bounds, PixelRect WorkArea, int Dpi, bool IsPrimary)
{
    public double Scale => Dpi / 96.0;

    /// <summary>Converts device-independent pixels (1/96 inch) to physical pixels on this monitor.</summary>
    public int ToPixels(double dip) => (int)Math.Round(dip * Scale, MidpointRounding.AwayFromZero);
}

public interface IMonitorProvider
{
    IReadOnlyList<MonitorInfo> GetMonitors();
}
