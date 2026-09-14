namespace MinkQuickLax.Core.Layout;

/// <summary>Finds the nearest free spot when an item is dropped on top of another (SPEC 4.4).</summary>
public static class CollisionResolver
{
    /// <summary>How far to search, in grid steps, before giving up and keeping the desired spot.</summary>
    public const int MaxRings = 60;

    public static bool IsFree(PixelRect rect, IReadOnlyList<PixelRect> occupied, PixelRect area) =>
        area.Contains(rect) && !occupied.Any(o => o.IntersectsWith(rect));

    /// <summary>
    /// Returns <paramref name="desiredCenter"/> (moved inside the area) if the item fits there; otherwise the
    /// closest free center on the grid around it. Falls back to the desired spot when nothing is free.
    /// </summary>
    public static PixelPoint FindFreeCenter(PixelPoint desiredCenter, PixelSize size, IReadOnlyList<PixelRect> occupied, PixelRect area, int gridPx)
    {
        var start = PixelRect.FromCenter(desiredCenter, size).MoveInside(area).Center;
        if (IsFree(PixelRect.FromCenter(start, size), occupied, area))
        {
            return start;
        }

        var step = Math.Max(gridPx, 1);
        PixelPoint? best = null;
        var bestDistance = double.MaxValue;
        // Ring n holds candidates n..n*sqrt(2) steps away, so stop once a ring cannot beat the best found.
        for (var ring = 1; ring <= MaxRings && ring * step < bestDistance; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                var edgeRow = Math.Abs(dy) == ring;
                for (var dx = -ring; dx <= ring; dx += edgeRow ? 1 : 2 * ring)
                {
                    var candidate = start.Offset(dx * step, dy * step);
                    var distance = candidate.DistanceTo(start);
                    if (distance < bestDistance && IsFree(PixelRect.FromCenter(candidate, size), occupied, area))
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            }
        }
        return best ?? start;
    }
}
