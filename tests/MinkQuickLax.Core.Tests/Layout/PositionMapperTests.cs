using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Layout;

public sealed class PositionMapperTests
{
    [Theory]
    [InlineData(96, 48, 72)]
    [InlineData(144, 48, 108)]
    [InlineData(192, 48, 144)]
    [InlineData(144, 32, 84)]
    [InlineData(120, 64, 110)]
    public void WindowSize_ScalesWithDpi(int dpi, int iconSize, int expected)
    {
        Assert.Equal(PixelSize.Square(expected), PositionMapper.WindowSize(iconSize, Monitors.Primary(dpi)));
    }

    [Theory]
    [InlineData(96)]
    [InlineData(144)]
    [InlineData(192)]
    public void FractionToPixels_UsesTheWorkArea(int dpi)
    {
        var monitor = Monitors.Primary(dpi);
        var placement = new Placement { Monitor = monitor.Id, X = 0.5, Y = 0.25 };

        var position = PositionMapper.ToScreen(placement, 48, [monitor], allowOverTaskbar: false);

        Assert.Equal(new PixelPoint(960, 258), position.Center);
        Assert.False(position.IsFallback);
        Assert.Equal(PositionMapper.WindowSize(48, monitor), position.Rect.Size);
    }

    [Theory]
    [InlineData(96)]
    [InlineData(144)]
    [InlineData(192)]
    public void PixelsToFraction_RoundTrips(int dpi)
    {
        var monitor = Monitors.Left(dpi);
        var center = new PixelPoint(-1000, 333);

        var placement = PositionMapper.WithCenter(new Placement(), center, monitor, allowOverTaskbar: false);
        var back = PositionMapper.ToScreen(placement, 48, [Monitors.Primary(), monitor], allowOverTaskbar: false);

        Assert.Equal(monitor.Id, placement.Monitor);
        Assert.Equal(monitor.EdidKey, placement.MonitorEdid);
        Assert.InRange(back.Center.X, center.X - 1, center.X + 1);
        Assert.InRange(back.Center.Y, center.Y - 1, center.Y + 1);
    }

    [Fact]
    public void SameFraction_LandsInTheSameRelativeSpotAfterResolutionChange()
    {
        var placement = new Placement { Monitor = Monitors.Primary().Id, X = 0.75, Y = 0.5 };
        var small = Monitors.Primary();
        var big = small with { Bounds = new PixelRect(0, 0, 3840, 2160), WorkArea = new PixelRect(0, 0, 3840, 2088), Dpi = 192 };

        var before = PositionMapper.ToScreen(placement, 48, [small], false);
        var after = PositionMapper.ToScreen(placement, 48, [big], false);

        Assert.Equal(1440, before.Center.X);
        Assert.Equal(2880, after.Center.X);
        Assert.Equal(1044, after.Center.Y);
    }

    [Fact]
    public void IconAtTheEdge_IsPulledInsideTheWorkArea()
    {
        var monitor = Monitors.Primary();
        var placement = new Placement { Monitor = monitor.Id, X = 1, Y = 1 };

        var rect = PositionMapper.ToScreen(placement, 48, [monitor], false).Rect;

        Assert.Equal(1920, rect.Right);
        Assert.Equal(1032, rect.Bottom); // above the taskbar
    }

    [Fact]
    public void AllowOverTaskbar_UsesTheWholeMonitor()
    {
        var monitor = Monitors.Primary();
        var placement = new Placement { Monitor = monitor.Id, X = 1, Y = 1 };

        var rect = PositionMapper.ToScreen(placement, 48, [monitor], allowOverTaskbar: true).Rect;

        Assert.Equal(1080, rect.Bottom);
    }

    [Fact]
    public void MissingMonitor_ShowsOnPrimary_AsFallback()
    {
        var placement = new Placement { Monitor = "gone", MonitorEdid = "ZZZ-9999-0", X = 0.1, Y = 0.9 };

        var position = PositionMapper.ToScreen(placement, 48, [Monitors.Left(), Monitors.Primary()], false);

        Assert.True(position.IsFallback);
        Assert.True(position.Monitor.IsPrimary);
        Assert.Equal(new PixelPoint(192, 929), position.Center);
    }

    [Fact]
    public void MonitorOnAnotherPort_IsFoundByEdid()
    {
        var moved = Monitors.Left(id: "new-port-path");
        var placement = new Placement { Monitor = Monitors.Left().Id, MonitorEdid = moved.EdidKey, X = 0.5, Y = 0.5 };

        var position = PositionMapper.ToScreen(placement, 48, [Monitors.Primary(), moved], false);

        Assert.False(position.IsFallback);
        Assert.Same(moved, position.Monitor);
    }

    [Fact]
    public void TwoIdenticalMonitorsWithoutSerial_AreNotGuessed()
    {
        var a = Monitors.Left(id: "a", edid: "SAME-MODEL-");
        var b = Monitors.Primary(id: "b", edid: "SAME-MODEL-");

        Assert.Null(PositionMapper.FindMonitor("old-path", "SAME-MODEL-", [a, b]));
    }

    [Fact]
    public void MonitorAt_FindsContainingOrNearest()
    {
        var left = Monitors.Left();
        var primary = Monitors.Primary();

        Assert.Same(left, PositionMapper.MonitorAt(new PixelPoint(-5, 10), [primary, left]));
        Assert.Same(primary, PositionMapper.MonitorAt(new PixelPoint(3000, 500), [primary, left]));
    }

    [Fact]
    public void NoMonitors_Throws()
    {
        Assert.Throws<ArgumentException>(() => PositionMapper.ToScreen(new Placement(), 48, Array.Empty<MonitorInfo>(), false));
    }
}
