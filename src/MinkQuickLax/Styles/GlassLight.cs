using System.Windows;
using System.Windows.Media;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Styles;

/// <summary>
/// The light on a Glass sheet (SPEC 5.2): a soft glow in the top-left corner and a smaller one in the bottom-right, a dark
/// hairline around the outside, a rim that catches the light at those two corners, and a top highlight that fades to the
/// right. Drawn over the sheet's fill by <see cref="SurfaceFrame"/> and <see cref="GlassLightLayer"/>.
/// </summary>
public static class GlassLight
{
    /// <summary>The top-left glow reaches this far: half the short side plus a quarter of the long side, within these limits.</summary>
    public const double SheenMinRadius = 40;
    public const double SheenMaxRadius = 260;

    /// <summary>The bottom-right glow, as a share of the top-left one, and its strength.</summary>
    public const double GlintRadiusRatio = 0.45;
    public const double GlintStrength = 0.6;

    /// <summary>The top highlight is gone by this fraction of the width.</summary>
    public const double HighlightFadeEnd = 0.7;

    public static void Draw(DrawingContext dc, Size size, double radius, Brush? hairline, Brush? rim, Brush? highlight, Brush? sheen)
    {
        if (size.Width <= 2 || size.Height <= 2)
        {
            return;
        }
        radius = Math.Min(radius, Math.Min(size.Width, size.Height) / 2);

        if (sheen is SolidColorBrush { Color.A: > 0 } glow)
        {
            var reach = Math.Clamp(Math.Min(size.Width, size.Height) / 2 + Math.Max(size.Width, size.Height) / 4, SheenMinRadius, SheenMaxRadius);
            dc.PushClip(new RectangleGeometry(new Rect(size), radius, radius));
            dc.DrawRectangle(Glow(glow.Color, new Point(0, 0), reach, 1), null, new Rect(size));
            dc.DrawRectangle(Glow(glow.Color, new Point(size.Width, size.Height), reach * GlintRadiusRatio, GlintStrength), null, new Rect(size));
            dc.Pop();
        }
        if (hairline is not null)
        {
            dc.DrawRoundedRectangle(null, new Pen(hairline, 1), Inset(size, 0.5), radius - 0.5, radius - 0.5);
        }
        if (rim is not null)
        {
            dc.DrawRoundedRectangle(null, new Pen(rim, 1), Inset(size, 1.5), Math.Max(0, radius - 1.5), Math.Max(0, radius - 1.5));
        }
        if (highlight is SolidColorBrush { Color.A: > 0 } top)
        {
            var fade = new LinearGradientBrush(
                [new GradientStop(top.Color, 0), new GradientStop(Color.FromArgb(0, top.Color.R, top.Color.G, top.Color.B), HighlightFadeEnd)],
                new Point(0, 0), new Point(1, 0));
            fade.Freeze();
            dc.DrawRectangle(fade, null, new Rect(radius, 1, Math.Max(0, size.Width - 2 * radius), 1));
        }
    }

    /// <summary>
    /// The rim: <paramref name="bright"/> from the top-left corner along about a third of the top and left edges,
    /// <paramref name="dim"/> around the other two corners, and bright again, a little weaker, at the bottom-right.
    /// </summary>
    public static Brush Rim(Color bright, Color dim)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(bright, 0));
        brush.GradientStops.Add(new GradientStop(Scale(bright, 0.7), 0.16));
        brush.GradientStops.Add(new GradientStop(dim, 0.42));
        brush.GradientStops.Add(new GradientStop(dim, 0.64));
        brush.GradientStops.Add(new GradientStop(Scale(bright, 0.45), 0.86));
        brush.GradientStops.Add(new GradientStop(Scale(bright, 0.75), 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>A row or button edge lit from above: <paramref name="bright"/> at the top, <paramref name="dim"/> at the bottom.</summary>
    public static Brush TopLit(Color bright, Color dim)
    {
        var brush = new LinearGradientBrush(bright, dim, new Point(0, 0), new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    /// <summary>A gloss over the top of a solid button: <paramref name="top"/> fading out by <paramref name="fadeEnd"/> of the height.</summary>
    public static Brush Gloss(Color top, double fadeEnd)
    {
        var brush = new LinearGradientBrush(
            [new GradientStop(top, 0), new GradientStop(Scale(top, 0), fadeEnd)],
            new Point(0, 0), new Point(0, 1));
        brush.Freeze();
        return brush;
    }

    public static Color Scale(Color color, double alphaFactor) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(color.A * alphaFactor, 0, 255)), color.R, color.G, color.B);

    private static Rect Inset(Size size, double inset) =>
        new(inset, inset, Math.Max(0, size.Width - 2 * inset), Math.Max(0, size.Height - 2 * inset));

    private static RadialGradientBrush Glow(Color color, Point center, double reach, double strength)
    {
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = center,
            GradientOrigin = center,
            RadiusX = reach,
            RadiusY = reach,
        };
        brush.GradientStops.Add(new GradientStop(Scale(color, strength), 0));
        brush.GradientStops.Add(new GradientStop(Scale(color, strength * 0.5), 0.35));
        brush.GradientStops.Add(new GradientStop(Scale(color, 0), 1));
        brush.Freeze();
        return brush;
    }
}

/// <summary>
/// <see cref="GlassLight"/> over a rounded sheet that is not a <see cref="SurfaceFrame"/>, such as a folder on the desktop.
/// Draws nothing outside Glass.
/// </summary>
public sealed class GlassLightLayer : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = Skin.KindProperty.AddOwner(
        typeof(GlassLightLayer), new FrameworkPropertyMetadata(StyleSetting.Glass, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusProperty = Register<double>(nameof(Radius), 0.0);
    public static readonly DependencyProperty HairlineProperty = Register<Brush?>(nameof(Hairline), null);
    public static readonly DependencyProperty RimProperty = Register<Brush?>(nameof(Rim), null);
    public static readonly DependencyProperty HighlightProperty = Register<Brush?>(nameof(Highlight), null);
    public static readonly DependencyProperty SheenProperty = Register<Brush?>(nameof(Sheen), null);

    public GlassLightLayer()
    {
        IsHitTestVisible = false;
        SetResourceReference(HairlineProperty, "Skin.Hairline");
        SetResourceReference(RimProperty, "Skin.Rim");
        SetResourceReference(HighlightProperty, "Skin.Highlight");
        SetResourceReference(SheenProperty, "Skin.Sheen");
    }

    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public Brush? Hairline
    {
        get => (Brush?)GetValue(HairlineProperty);
        set => SetValue(HairlineProperty, value);
    }

    public Brush? Rim
    {
        get => (Brush?)GetValue(RimProperty);
        set => SetValue(RimProperty, value);
    }

    public Brush? Highlight
    {
        get => (Brush?)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public Brush? Sheen
    {
        get => (Brush?)GetValue(SheenProperty);
        set => SetValue(SheenProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if ((StyleSetting)GetValue(KindProperty) == StyleSetting.Glass)
        {
            GlassLight.Draw(drawingContext, RenderSize, Radius, Hairline, Rim, Highlight, Sheen);
        }
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(GlassLightLayer), new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
