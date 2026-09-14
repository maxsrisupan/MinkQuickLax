using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Layout;

/// <param name="Vertical">True for a vertical line at x = <paramref name="Position"/>.</param>
/// <param name="Start">Where the line starts along its direction.</param>
/// <param name="End">Where the line ends along its direction.</param>
public sealed record GuideLine(bool Vertical, int Position, int Start, int End);

public sealed record SnapResult(PixelPoint Center, IReadOnlyList<GuideLine> Guides);

/// <summary>Snaps a dragged item to other items' edges/centers, otherwise to the grid (SPEC 4.4).</summary>
public static class SnapEngine
{
    public static SnapResult Snap(
        PixelPoint desiredCenter,
        PixelSize size,
        IReadOnlyList<PixelRect> others,
        PixelRect area,
        SnapMode mode,
        int gridPx,
        int alignThresholdPx)
    {
        if (mode == SnapMode.None)
        {
            return new SnapResult(PixelRect.FromCenter(desiredCenter, size).MoveInside(area).Center, []);
        }

        var x = desiredCenter.X;
        var y = desiredCenter.Y;
        PixelRect? alignedToX = null;
        PixelRect? alignedToY = null;
        var lineX = 0;
        var lineY = 0;

        if (mode == SnapMode.GridAndAlign)
        {
            (alignedToX, x, lineX) = Align(desiredCenter.X, size.Width, others, r => (r.Left, r.Center.X, r.Right), alignThresholdPx);
            (alignedToY, y, lineY) = Align(desiredCenter.Y, size.Height, others, r => (r.Top, r.Center.Y, r.Bottom), alignThresholdPx);
        }
        if (alignedToX is null)
        {
            x = area.Left + RoundToGrid(desiredCenter.X - area.Left, gridPx);
        }
        if (alignedToY is null)
        {
            y = area.Top + RoundToGrid(desiredCenter.Y - area.Top, gridPx);
        }

        var wanted = PixelRect.FromCenter(new PixelPoint(x, y), size);
        var rect = wanted.MoveInside(area);
        var guides = new List<GuideLine>(2);
        if (alignedToX is { } ox && rect.Left == wanted.Left)
        {
            guides.Add(new GuideLine(true, lineX, Math.Min(ox.Top, rect.Top), Math.Max(ox.Bottom, rect.Bottom)));
        }
        if (alignedToY is { } oy && rect.Top == wanted.Top)
        {
            guides.Add(new GuideLine(false, lineY, Math.Min(oy.Left, rect.Left), Math.Max(oy.Right, rect.Right)));
        }
        return new SnapResult(rect.Center, guides);
    }

    public static int RoundToGrid(int value, int grid) =>
        grid <= 1 ? value : (int)Math.Round(value / (double)grid, MidpointRounding.AwayFromZero) * grid;

    /// <summary>
    /// Finds the closest match along one axis within the threshold: center to center, start to start, end to end,
    /// or edge to opposite edge (side by side). Centers come first so equal-size items show the guide in the middle.
    /// </summary>
    private static (PixelRect? Other, int Center, int Line) Align(
        int desiredCenter,
        int length,
        IReadOnlyList<PixelRect> others,
        Func<PixelRect, (int Start, int Middle, int End)> axis,
        int threshold)
    {
        var toStart = -(length / 2);
        var toEnd = length - length / 2;
        PixelRect? best = null;
        var bestDistance = threshold + 1;
        var bestCenter = desiredCenter;
        var bestLine = 0;
        foreach (var other in others)
        {
            var (start, middle, end) = axis(other);
            ReadOnlySpan<(int Offset, int Line)> pairs = [(0, middle), (toStart, start), (toEnd, end), (toStart, end), (toEnd, start)];
            foreach (var (offset, line) in pairs)
            {
                var distance = Math.Abs(desiredCenter + offset - line);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                    bestCenter = line - offset;
                    bestLine = line;
                }
            }
        }
        return (best, bestCenter, bestLine);
    }
}
