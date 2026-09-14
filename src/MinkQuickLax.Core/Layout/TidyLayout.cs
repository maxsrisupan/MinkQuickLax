namespace MinkQuickLax.Core.Layout;

/// <summary>"Tidy up": items in a grid from the top-right corner, filling each column downward, then moving left (SPEC 4.4).</summary>
public static class TidyLayout
{
    /// <summary>Centers for <paramref name="count"/> items of size <paramref name="cell"/>, in order.</summary>
    public static IReadOnlyList<PixelPoint> Slots(int count, PixelSize cell, PixelRect area, int gridPx)
    {
        var slots = new List<PixelPoint>(count);
        foreach (var slot in Enumerate(cell, area, gridPx))
        {
            if (slots.Count == count)
            {
                break;
            }
            slots.Add(slot);
        }
        return slots;
    }

    /// <summary>The first <paramref name="count"/> tidy slots that do not overlap <paramref name="occupied"/>.</summary>
    public static IReadOnlyList<PixelPoint> FreeSlots(int count, PixelSize cell, PixelRect area, int gridPx, IReadOnlyList<PixelRect> occupied)
    {
        var taken = occupied.ToList();
        var slots = new List<PixelPoint>(count);
        foreach (var slot in Enumerate(cell, area, gridPx))
        {
            if (slots.Count == count)
            {
                break;
            }
            var rect = PixelRect.FromCenter(slot, cell);
            if (taken.Any(t => t.IntersectsWith(rect)))
            {
                continue;
            }
            slots.Add(slot);
            taken.Add(rect);
        }
        return slots;
    }

    private static IEnumerable<PixelPoint> Enumerate(PixelSize cell, PixelRect area, int gridPx)
    {
        var margin = Math.Max(gridPx, 0);
        var step = Math.Max(gridPx, 1);
        var rows = Math.Max(1, (area.Height - 2 * margin) / Math.Max(cell.Height, 1));

        // Align to the same grid the snap engine uses, rounding inward so items stay inside the margin.
        var right = area.Right - margin - (cell.Width - cell.Width / 2);
        var firstX = area.Left + (right - area.Left) / step * step;
        var top = area.Top + margin + cell.Height / 2;
        var firstY = area.Top + (top - area.Top + step - 1) / step * step;

        // Endless: past the left edge the columns keep going, so every item still gets a distinct slot.
        for (var i = 0; ; i++)
        {
            yield return new PixelPoint(firstX - i / rows * cell.Width, firstY + i % rows * cell.Height);
        }
    }
}
