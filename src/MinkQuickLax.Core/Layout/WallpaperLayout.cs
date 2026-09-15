namespace MinkQuickLax.Core.Layout;

/// <summary>How Windows fits a desktop wallpaper to a monitor (Settings › Personalization › Background).</summary>
public enum WallpaperFit
{
    Center,
    Tile,
    Stretch,
    Fit,
    Fill,
    Span,
}

/// <summary>One monitor's desktop background: an image placed with a fit, over a solid color.</summary>
/// <param name="Monitor">The monitor in physical pixels.</param>
/// <param name="ImagePath">The image file, or null when the background is a solid color.</param>
/// <param name="BackgroundRgb">The solid color behind the image, as 0xRRGGBB.</param>
public sealed record DesktopBackground(PixelRect Monitor, string? ImagePath, WallpaperFit Fit, int BackgroundRgb);

/// <summary>Where Windows draws a wallpaper image, so the Glass frost can blur the same picture (SPEC 5.5).</summary>
public static class WallpaperLayout
{
    /// <summary>
    /// The rectangle the image covers, in the same pixels as <paramref name="monitor"/>. For <see cref="WallpaperFit.Tile"/>
    /// it is the first tile at the monitor's top-left; the others repeat from there. <see cref="WallpaperFit.Span"/> fills
    /// <paramref name="desktop"/>, the bounds of all monitors, so each monitor shows its own part of one picture.
    /// </summary>
    public static PixelRect Place(WallpaperFit fit, PixelRect monitor, PixelSize image, PixelRect desktop)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return monitor;
        }
        return fit switch
        {
            WallpaperFit.Stretch => monitor,
            WallpaperFit.Tile => new PixelRect(monitor.Left, monitor.Top, monitor.Left + image.Width, monitor.Top + image.Height),
            WallpaperFit.Center => PixelRect.FromCenter(monitor.Center, image),
            WallpaperFit.Fit => Scaled(monitor, image, cover: false),
            WallpaperFit.Span => Scaled(desktop, image, cover: true),
            _ => Scaled(monitor, image, cover: true),
        };
    }

    private static PixelRect Scaled(PixelRect area, PixelSize image, bool cover)
    {
        var sx = (double)area.Width / image.Width;
        var sy = (double)area.Height / image.Height;
        var scale = cover ? Math.Max(sx, sy) : Math.Min(sx, sy);
        var size = new PixelSize((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * scale));
        return PixelRect.FromCenter(area.Center, size);
    }
}
