namespace MinkQuickLax.Core.Layout;

/// <summary>A point in physical screen pixels (virtual desktop coordinates).</summary>
public readonly record struct PixelPoint(int X, int Y)
{
    public double DistanceTo(PixelPoint other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public PixelPoint Offset(int dx, int dy) => new(X + dx, Y + dy);
}

public readonly record struct PixelSize(int Width, int Height)
{
    public static PixelSize Square(int side) => new(side, side);
}

/// <summary>A rectangle in physical pixels; Right and Bottom are exclusive.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public PixelSize Size => new(Width, Height);
    public PixelPoint Center => new(Left + Width / 2, Top + Height / 2);

    public static PixelRect FromCenter(PixelPoint center, PixelSize size)
    {
        var left = center.X - size.Width / 2;
        var top = center.Y - size.Height / 2;
        return new PixelRect(left, top, left + size.Width, top + size.Height);
    }

    public bool Contains(PixelPoint p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    public bool Contains(PixelRect r) => r.Left >= Left && r.Right <= Right && r.Top >= Top && r.Bottom <= Bottom;

    /// <summary>True when the rectangles share area; touching edges do not count.</summary>
    public bool IntersectsWith(PixelRect r) => Left < r.Right && r.Left < Right && Top < r.Bottom && r.Top < Bottom;

    public PixelRect Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);

    /// <summary>Moves this rectangle the least distance needed to lie inside <paramref name="area"/> (top-left wins if it is larger).</summary>
    public PixelRect MoveInside(PixelRect area)
    {
        var dx = Right > area.Right ? area.Right - Right : 0;
        var dy = Bottom > area.Bottom ? area.Bottom - Bottom : 0;
        var moved = Offset(dx, dy);
        dx = moved.Left < area.Left ? area.Left - moved.Left : 0;
        dy = moved.Top < area.Top ? area.Top - moved.Top : 0;
        return moved.Offset(dx, dy);
    }

    /// <summary>Distance from <paramref name="p"/> to the nearest point of this rectangle (0 inside).</summary>
    public double DistanceTo(PixelPoint p)
    {
        var dx = Math.Max(Math.Max(Left - p.X, 0), p.X - (Right - 1));
        var dy = Math.Max(Math.Max(Top - p.Y, 0), p.Y - (Bottom - 1));
        return Math.Sqrt((double)dx * dx + (double)dy * dy);
    }
}
