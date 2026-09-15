using MinkQuickLax.Core.Layout;

namespace MinkQuickLax.Core.Tests.Layout;

public sealed class WallpaperLayoutTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void Fill_CoversTheMonitorAndCropsTheLongSide()
    {
        // A 4:3 picture on a 16:9 monitor: as wide as the monitor, cut at the top and bottom.
        var rect = WallpaperLayout.Place(WallpaperFit.Fill, Monitor, new PixelSize(1600, 1200), Monitor);

        Assert.Equal(new PixelRect(0, -180, 1920, 1260), rect);
    }

    [Fact]
    public void Fit_ShowsTheWholePictureWithBars()
    {
        var rect = WallpaperLayout.Place(WallpaperFit.Fit, Monitor, new PixelSize(1600, 1200), Monitor);

        Assert.Equal(new PixelRect(240, 0, 1680, 1080), rect);
    }

    [Fact]
    public void Stretch_IsTheMonitor()
    {
        Assert.Equal(Monitor, WallpaperLayout.Place(WallpaperFit.Stretch, Monitor, new PixelSize(800, 800), Monitor));
    }

    [Fact]
    public void Center_KeepsTheSizeInTheMiddle()
    {
        var second = new PixelRect(1920, 0, 3840, 1080);

        var rect = WallpaperLayout.Place(WallpaperFit.Center, second, new PixelSize(800, 600), second);

        Assert.Equal(new PixelRect(2480, 240, 3280, 840), rect);
    }

    [Fact]
    public void Tile_StartsAtTheMonitorCorner()
    {
        var second = new PixelRect(1920, -200, 3840, 880);

        var rect = WallpaperLayout.Place(WallpaperFit.Tile, second, new PixelSize(256, 256), second);

        Assert.Equal(new PixelRect(1920, -200, 2176, 56), rect);
    }

    [Fact]
    public void Span_FillsTheWholeDesktop()
    {
        var desktop = new PixelRect(0, 0, 3840, 1080);

        var rect = WallpaperLayout.Place(WallpaperFit.Span, new PixelRect(1920, 0, 3840, 1080), new PixelSize(3840, 1080), desktop);

        Assert.Equal(desktop, rect);
    }

    [Fact]
    public void EmptyImage_CoversTheMonitor()
    {
        Assert.Equal(Monitor, WallpaperLayout.Place(WallpaperFit.Fill, Monitor, new PixelSize(0, 0), Monitor));
    }
}
