using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Layout;

public sealed class SnapEngineTests
{
    private static readonly PixelRect Area = new(0, 0, 1920, 1032);
    private static readonly PixelSize Icon = PixelSize.Square(72);

    [Fact]
    public void None_KeepsThePointer_ButStaysInside()
    {
        var result = SnapEngine.Snap(new PixelPoint(13, 500), Icon, [], Area, SnapMode.None, 24, 8);

        Assert.Equal(new PixelPoint(36, 500), result.Center);
        Assert.Empty(result.Guides);
    }

    [Theory]
    [InlineData(500, 511, 504, 504)]
    [InlineData(1000, 911, 1008, 912)]
    [InlineData(100, 107, 96, 96)]
    public void Grid_RoundsToTheNearestGridLine(int x, int y, int expectedX, int expectedY)
    {
        var result = SnapEngine.Snap(new PixelPoint(x, y), Icon, [], Area, SnapMode.Grid, 24, 8);

        Assert.Equal(new PixelPoint(expectedX, expectedY), result.Center);
    }

    [Fact]
    public void Grid_IsRelativeToTheAreaOrigin()
    {
        var area = new PixelRect(-2560, -200, 0, 1240);

        var result = SnapEngine.Snap(new PixelPoint(-1000, 101), Icon, [], area, SnapMode.Grid, 36, 12);

        Assert.Equal(0, (result.Center.X - area.Left) % 36);
        Assert.Equal(0, (result.Center.Y - area.Top) % 36);
    }

    [Fact]
    public void GridAndAlign_AlignsCentersWithinThreshold_AndReportsAGuide()
    {
        var other = PixelRect.FromCenter(new PixelPoint(407, 200), Icon);

        var result = SnapEngine.Snap(new PixelPoint(400, 600), Icon, [other], Area, SnapMode.GridAndAlign, 24, 8);

        Assert.Equal(407, result.Center.X); // aligned, not on the grid
        Assert.Equal(600, result.Center.Y); // grid
        var guide = Assert.Single(result.Guides);
        Assert.True(guide.Vertical);
        Assert.Equal(407, guide.Position);
        Assert.Equal(other.Top, guide.Start);
        Assert.Equal(636, guide.End);
    }

    [Fact]
    public void GridAndAlign_BeyondThreshold_FallsBackToGrid()
    {
        var other = PixelRect.FromCenter(new PixelPoint(430, 200), Icon);

        var result = SnapEngine.Snap(new PixelPoint(400, 600), Icon, [other], Area, SnapMode.GridAndAlign, 24, 8);

        Assert.Equal(new PixelPoint(408, 600), result.Center);
        Assert.Empty(result.Guides);
    }

    [Fact]
    public void GridAndAlign_PicksTheClosestOfSeveral()
    {
        var far = PixelRect.FromCenter(new PixelPoint(806, 100), Icon);
        var near = PixelRect.FromCenter(new PixelPoint(801, 900), Icon);

        var result = SnapEngine.Snap(new PixelPoint(800, 500), Icon, [far, near], Area, SnapMode.GridAndAlign, 24, 8);

        Assert.Equal(801, result.Center.X);
    }

    [Fact]
    public void GridAndAlign_AlignsEdgesOfDifferentSizes()
    {
        var wide = new PixelRect(100, 100, 400, 172); // a bar-shaped item

        var result = SnapEngine.Snap(new PixelPoint(441, 300), Icon, [wide], Area, SnapMode.GridAndAlign, 24, 8);

        // Our left edge (center - 36) meets its right edge at 400.
        Assert.Equal(436, result.Center.X);
    }

    [Fact]
    public void GridAndAlign_OnATie_PrefersCentersOverANeighboursEdge()
    {
        // Icons on a 72 px pitch: the one to the left touches our left edge at the same distance as the one above
        // lines up with our center. The guide should run through the centers.
        var left = PixelRect.FromCenter(new PixelPoint(384, 204), Icon);
        var above = PixelRect.FromCenter(new PixelPoint(456, 132), Icon);

        var result = SnapEngine.Snap(new PixelPoint(458, 314), Icon, [left, above], Area, SnapMode.GridAndAlign, 24, 8);

        Assert.Equal(456, result.Center.X);
        var guide = Assert.Single(result.Guides, g => g.Vertical);
        Assert.Equal(456, guide.Position);
    }

    [Fact]
    public void ClampedAtTheEdge_DropsThatGuide()
    {
        var other = PixelRect.FromCenter(new PixelPoint(1900, 500), PixelSize.Square(20));

        var result = SnapEngine.Snap(new PixelPoint(1897, 900), Icon, [other], Area, SnapMode.GridAndAlign, 24, 8);

        Assert.Equal(1884, result.Center.X);
        Assert.Empty(result.Guides);
    }
}

public sealed class CollisionResolverTests
{
    private static readonly PixelRect Area = new(0, 0, 1920, 1032);
    private static readonly PixelSize Icon = PixelSize.Square(72);

    [Fact]
    public void FreeSpot_IsKept()
    {
        var occupied = new[] { PixelRect.FromCenter(new PixelPoint(100, 100), Icon) };

        Assert.Equal(new PixelPoint(500, 500), CollisionResolver.FindFreeCenter(new PixelPoint(500, 500), Icon, occupied, Area, 24));
    }

    [Fact]
    public void TouchingEdges_DoNotCount()
    {
        var occupied = new[] { PixelRect.FromCenter(new PixelPoint(500, 500), Icon) };

        Assert.Equal(new PixelPoint(572, 500), CollisionResolver.FindFreeCenter(new PixelPoint(572, 500), Icon, occupied, Area, 24));
    }

    [Fact]
    public void DroppedOnTop_MovesToTheNearestFreeGridSpot()
    {
        var occupied = new[] { PixelRect.FromCenter(new PixelPoint(500, 500), Icon) };

        var spot = CollisionResolver.FindFreeCenter(new PixelPoint(510, 500), Icon, occupied, Area, 24);

        // Three grid steps (one icon width) away, in whichever direction is found first.
        Assert.Equal(72, spot.DistanceTo(new PixelPoint(510, 500)));
        Assert.False(occupied[0].IntersectsWith(PixelRect.FromCenter(spot, Icon)));
    }

    [Fact]
    public void PrefersACloserDiagonalOverAFartherStraightLine()
    {
        // A wall of icons left, right, up and down; only the diagonal corners are open.
        var start = new PixelPoint(960, 516);
        var occupied = new List<PixelRect>();
        for (var i = -4; i <= 4; i++)
        {
            occupied.Add(PixelRect.FromCenter(start.Offset(i * 24, 0), Icon));
            occupied.Add(PixelRect.FromCenter(start.Offset(0, i * 24), Icon));
        }

        var spot = CollisionResolver.FindFreeCenter(start, Icon, occupied, Area, 24);

        Assert.Equal(72, Math.Abs(spot.X - start.X));
        Assert.Equal(72, Math.Abs(spot.Y - start.Y));
    }

    [Fact]
    public void NearTheEdge_StaysInsideTheArea()
    {
        var occupied = new[] { PixelRect.FromCenter(new PixelPoint(1884, 36), Icon) };

        var spot = CollisionResolver.FindFreeCenter(new PixelPoint(1919, 0), Icon, occupied, Area, 24);

        Assert.True(Area.Contains(PixelRect.FromCenter(spot, Icon)));
        Assert.False(occupied[0].IntersectsWith(PixelRect.FromCenter(spot, Icon)));
    }

    [Fact]
    public void NothingFree_KeepsTheDesiredSpot()
    {
        var tiny = new PixelRect(0, 0, 100, 100);
        var occupied = new[] { tiny };

        Assert.Equal(new PixelPoint(50, 50), CollisionResolver.FindFreeCenter(new PixelPoint(50, 50), Icon, occupied, tiny, 24));
    }
}

public sealed class TidyLayoutTests
{
    private static readonly PixelRect Work = new(0, 0, 1920, 1032);
    private static readonly PixelSize Cell = PixelSize.Square(72);

    [Fact]
    public void FirstSlot_IsTheTopRightCorner_InsideTheMargin()
    {
        var first = TidyLayout.Slots(1, Cell, Work, 24)[0];
        var rect = PixelRect.FromCenter(first, Cell);

        Assert.InRange(Work.Right - rect.Right, 24, 47);
        Assert.InRange(rect.Top - Work.Top, 24, 47);
        Assert.Equal(0, first.X % 24);
        Assert.Equal(0, first.Y % 24);
    }

    [Fact]
    public void FillsColumnsDownward_ThenMovesLeft()
    {
        var rows = (1032 - 48) / 72;
        var slots = TidyLayout.Slots(rows + 2, Cell, Work, 24);

        Assert.Equal(slots[0].X, slots[rows - 1].X);
        Assert.Equal(slots[0].Y + 72, slots[1].Y);
        Assert.Equal(slots[0].X - 72, slots[rows].X);
        Assert.Equal(slots[0].Y, slots[rows].Y);
        Assert.All(slots, s => Assert.True(Work.Contains(PixelRect.FromCenter(s, Cell))));
    }

    [Fact]
    public void SlotsNeverOverlap()
    {
        var rects = TidyLayout.Slots(80, Cell, Work, 24).Select(s => PixelRect.FromCenter(s, Cell)).ToList();

        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                Assert.False(rects[i].IntersectsWith(rects[j]), $"{i} overlaps {j}");
            }
        }
    }

    [Fact]
    public void HighDpi_ScalesCellsAndGrid()
    {
        var work = new PixelRect(0, 0, 2880, 1548);
        var cell = PixelSize.Square(108);

        var slots = TidyLayout.Slots(3, cell, work, 36);

        Assert.All(slots, s => Assert.Equal(0, s.X % 36));
        Assert.Equal(108, slots[1].Y - slots[0].Y);
    }

    [Fact]
    public void FreeSlots_SkipOccupiedSpots()
    {
        var all = TidyLayout.Slots(3, Cell, Work, 24);
        var occupied = new[] { PixelRect.FromCenter(all[0], Cell), PixelRect.FromCenter(all[2].Offset(10, 0), Cell) };

        var free = TidyLayout.FreeSlots(2, Cell, Work, 24, occupied);

        Assert.Equal(all[1], free[0]);
        Assert.NotEqual(all[2], free[1]);
        Assert.All(free, f => Assert.DoesNotContain(occupied, o => o.IntersectsWith(PixelRect.FromCenter(f, Cell))));
    }
}
