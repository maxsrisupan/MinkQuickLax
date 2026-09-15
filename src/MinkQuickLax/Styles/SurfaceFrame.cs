using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Styles;

/// <summary>
/// The current style, inherited down the element tree from each window, so templates can switch on it with
/// <c>&lt;Trigger Property="styles:Skin.Kind" Value="Hud"&gt;</c>.
/// </summary>
public static class Skin
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.RegisterAttached(
        "Kind", typeof(StyleSetting), typeof(Skin),
        new FrameworkPropertyMetadata(StyleSetting.Glass, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public static StyleSetting GetKind(DependencyObject element) => (StyleSetting)element.GetValue(KindProperty);

    public static void SetKind(DependencyObject element, StyleSetting value) => element.SetValue(KindProperty, value);
}

public enum SurfaceShape
{
    /// <summary>Menus, notices, toolbar, panels, scanner, settings.</summary>
    Plate,

    /// <summary>The icon name: smaller corners; HUD marks the top-left and bottom-right instead of cutting.</summary>
    Tooltip,
}

/// <summary>
/// Draws a surface's plate in the current style (SPEC 5.2, 5.8, 5.9): Glass is a rounded tinted sheet lit from the top-left
/// (<see cref="GlassLight"/>); HUD is a dark plate with cut corners, cyan corner marks and scan lines; Dot Matrix is a solid rounded
/// plate. Everything outside the shape stays transparent. Layout (padding, child) is the usual <see cref="Border"/>.
/// </summary>
public sealed class SurfaceFrame : Border
{
    public static readonly DependencyProperty KindProperty =
        Skin.KindProperty.AddOwner(typeof(SurfaceFrame), new FrameworkPropertyMetadata(StyleSetting.Glass, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShapeProperty = Register<SurfaceShape>(nameof(Shape), SurfaceShape.Plate);
    public static readonly DependencyProperty FillProperty = Register<Brush?>(nameof(Fill), null);
    public static readonly DependencyProperty EdgeProperty = Register<Brush?>(nameof(Edge), null);
    public static readonly DependencyProperty HighlightProperty = Register<Brush?>(nameof(Highlight), null);
    public static readonly DependencyProperty HairlineProperty = Register<Brush?>(nameof(Hairline), null);
    public static readonly DependencyProperty RimProperty = Register<Brush?>(nameof(Rim), null);
    public static readonly DependencyProperty SheenProperty = Register<Brush?>(nameof(Sheen), null);
    public static readonly DependencyProperty BackdropProperty = Register<Brush?>(nameof(Backdrop), null);
    public static readonly DependencyProperty MarkProperty = Register<Brush?>(nameof(Mark), null);
    public static readonly DependencyProperty ScanLinesProperty = Register<Brush?>(nameof(ScanLines), null);

    private Brush? _scanBrush;
    private Color _scanColor;

    public SurfaceFrame()
    {
        SnapsToDevicePixels = true;
        SetResourceReference(EdgeProperty, "Skin.Edge");
        SetResourceReference(HighlightProperty, "Skin.Highlight");
        SetResourceReference(HairlineProperty, "Skin.Hairline");
        SetResourceReference(RimProperty, "Skin.Rim");
        SetResourceReference(SheenProperty, "Skin.Sheen");
        SetResourceReference(MarkProperty, "Accent");
        SetResourceReference(ScanLinesProperty, "Skin.ScanLines");
    }

    public StyleSetting Kind
    {
        get => (StyleSetting)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public SurfaceShape Shape
    {
        get => (SurfaceShape)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush? Edge
    {
        get => (Brush?)GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    public Brush? Highlight
    {
        get => (Brush?)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    /// <summary>Glass: dark line around the outside.</summary>
    public Brush? Hairline
    {
        get => (Brush?)GetValue(HairlineProperty);
        set => SetValue(HairlineProperty, value);
    }

    /// <summary>Glass: the inner edge that catches the light.</summary>
    public Brush? Rim
    {
        get => (Brush?)GetValue(RimProperty);
        set => SetValue(RimProperty, value);
    }

    /// <summary>Glass: color of the corner glows.</summary>
    public Brush? Sheen
    {
        get => (Brush?)GetValue(SheenProperty);
        set => SetValue(SheenProperty, value);
    }

    /// <summary>Glass without Windows blur: the frosted desktop picture under the tint (<see cref="Services.GlassFrost"/>).</summary>
    public Brush? Backdrop
    {
        get => (Brush?)GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    /// <summary>HUD corner marks (cyan).</summary>
    public Brush? Mark
    {
        get => (Brush?)GetValue(MarkProperty);
        set => SetValue(MarkProperty, value);
    }

    /// <summary>HUD scan-line color; transparent in the other styles.</summary>
    public Brush? ScanLines
    {
        get => (Brush?)GetValue(ScanLinesProperty);
        set => SetValue(ScanLinesProperty, value);
    }

    /// <summary>The plate outline for a given size, also used to clip content in HUD.</summary>
    public Geometry Outline(Size size)
    {
        var bounds = new Rect(size);
        switch (Kind)
        {
            case StyleSetting.Hud when Shape == SurfaceShape.Plate:
                var cut = Math.Min(HudDesign.SurfaceCut, Math.Min(size.Width, size.Height) / 2);
                var figure = new PathFigure { StartPoint = new Point(cut, 0), IsClosed = true, IsFilled = true };
                figure.Segments.Add(new PolyLineSegment(
                    [new Point(size.Width, 0), new Point(size.Width, size.Height - cut), new Point(size.Width - cut, size.Height), new Point(0, size.Height), new Point(0, cut)],
                    isStroked: true));
                var path = new PathGeometry([figure]);
                path.Freeze();
                return path;
            case StyleSetting.Hud:
                return new RectangleGeometry(bounds);
            default:
                var radii = Kind == StyleSetting.Dot ? StyleRadii.Dot : StyleRadii.Glass;
                var radius = Math.Min(Shape == SurfaceShape.Tooltip ? radii.Tooltip : radii.Surface, Math.Min(size.Width, size.Height) / 2);
                return new RectangleGeometry(bounds, radius, radius);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var outline = Outline(size);
        if (Kind == StyleSetting.Glass && Backdrop is { } backdrop)
        {
            dc.DrawGeometry(backdrop, null, outline);
        }
        dc.DrawGeometry(Fill, null, outline);

        if (Kind == StyleSetting.Hud)
        {
            RenderHud(dc, size, outline);
            return;
        }

        if (Kind == StyleSetting.Glass)
        {
            var radius = Shape == SurfaceShape.Tooltip ? StyleRadii.Glass.Tooltip : StyleRadii.Glass.Surface;
            GlassLight.Draw(dc, size, radius, Hairline, Rim ?? Edge, Highlight, Sheen);
            return;
        }

        // 1 px inner edge (SPEC 5.9).
        if (Edge is { } edge)
        {
            var inset = Outline(new Size(Math.Max(0, size.Width - 1), Math.Max(0, size.Height - 1))).Clone();
            inset.Transform = new TranslateTransform(0.5, 0.5);
            dc.DrawGeometry(null, new Pen(edge, 1), inset);
        }
    }

    private void RenderHud(DrawingContext dc, Size size, Geometry outline)
    {
        if (ScanLineBrush() is { } lines)
        {
            dc.DrawGeometry(lines, null, outline);
        }
        if (Edge is { } edge)
        {
            dc.PushClip(outline);
            dc.DrawGeometry(null, new Pen(edge, 2), outline);
            dc.Pop();
        }
        if (Mark is not { } mark)
        {
            return;
        }
        var (length, thickness) = Shape == SurfaceShape.Tooltip ? (7.0, 1.5) : (HudDesign.CornerAccentLength, HudDesign.CornerAccentThickness);
        if (Shape == SurfaceShape.Tooltip)
        {
            // Top-left and bottom-right marks.
            dc.DrawRectangle(mark, null, new Rect(0, 0, length, thickness));
            dc.DrawRectangle(mark, null, new Rect(0, 0, thickness, length));
            dc.DrawRectangle(mark, null, new Rect(size.Width - length, size.Height - thickness, length, thickness));
            dc.DrawRectangle(mark, null, new Rect(size.Width - thickness, size.Height - length, thickness, length));
        }
        else
        {
            // Top-right and bottom-left: the corners that are not cut.
            dc.DrawRectangle(mark, null, new Rect(size.Width - length, 0, length, thickness));
            dc.DrawRectangle(mark, null, new Rect(size.Width - thickness, 0, thickness, length));
            dc.DrawRectangle(mark, null, new Rect(0, size.Height - thickness, length, thickness));
            dc.DrawRectangle(mark, null, new Rect(0, size.Height - length, thickness, length));
        }
    }

    /// <summary>1 px horizontal lines every 3 px, cached per color.</summary>
    private Brush? ScanLineBrush()
    {
        if (ScanLines is not SolidColorBrush { Color.A: > 0 } solid)
        {
            return null;
        }
        if (_scanBrush is null || _scanColor != solid.Color)
        {
            var drawing = new GeometryDrawing(solid, null, new RectangleGeometry(new Rect(0, 0, 1, 1)));
            var brush = new DrawingBrush(new DrawingGroup { Children = { new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 1, HudDesign.ScanLinePeriod))), drawing } })
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 1, HudDesign.ScanLinePeriod),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.None,
            };
            brush.Freeze();
            _scanBrush = brush;
            _scanColor = solid.Color;
        }
        return _scanBrush;
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(SurfaceFrame), new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
