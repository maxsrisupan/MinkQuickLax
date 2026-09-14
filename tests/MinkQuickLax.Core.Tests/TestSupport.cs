using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Layout;

namespace MinkQuickLax.Core.Tests;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MinkQuickLax.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort; the OS temp cleanup will get it.
        }
    }
}

internal static class Monitors
{
    /// <summary>A 1920×1080 monitor at the origin with a 48 px taskbar at the bottom.</summary>
    public static MonitorInfo Primary(int dpi = 96, string id = @"\\?\DISPLAY#AAA0001#1&1&UID1#{guid}", string? edid = "AAA-0001-111") =>
        new(id, edid, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1032), dpi, IsPrimary: true);

    /// <summary>A 2560×1440 monitor to the left of the primary (negative coordinates), no taskbar.</summary>
    public static MonitorInfo Left(int dpi = 144, string id = @"\\?\DISPLAY#BBB0002#1&1&UID2#{guid}", string? edid = "BBB-0002-222") =>
        new(id, edid, new PixelRect(-2560, -200, 0, 1240), new PixelRect(-2560, -200, 0, 1240), dpi, IsPrimary: false);
}
