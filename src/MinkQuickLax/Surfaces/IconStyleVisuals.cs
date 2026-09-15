using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MinkQuickLax.Core.Imaging;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// The HUD icon plate (SPEC 5.8): a dark square with the top-left and bottom-right corners cut at 22%, a cyan tint
/// from the top-left, bright chamfer edges and an inner edge that brightens as the mouse comes near.
/// </summary>
public sealed class HudPlate : FrameworkElement
{
    public static readonly DependencyProperty NearnessProperty = DependencyProperty.Register(
        nameof(Nearness), typeof(double), typeof(HudPlate), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush PlateFill = Frozen(new SolidColorBrush(ThemeService.WithAlpha(HudDesign.Plate, HudDesign.IconPlateAlpha)));
    private static readonly Brush TintFill = Frozen(new LinearGradientBrush(
        [new GradientStop(ThemeService.WithAlpha(HudDesign.Cyan, 0.16), 0), new GradientStop(ThemeService.WithAlpha(HudDesign.Cyan, 0), 0.55)],
        new Point(0, 0), new Point(1, 1)));
    private static readonly Pen ChamferPen = Frozen(new Pen(new SolidColorBrush(ThemeService.WithAlpha(HudDesign.Cyan, 0.7)), 1.4));

    /// <summary>0 at rest, 1 when the mouse is on the icon.</summary>
    public double Nearness
    {
        get => (double)GetValue(NearnessProperty);
        set => SetValue(NearnessProperty, value);
    }

    public static Geometry Outline(Size size, double cutRatio = IconStyleDesign.HudIconCut)
    {
        var cut = Math.Min(size.Width, size.Height) * cutRatio;
        var figure = new PathFigure { StartPoint = new Point(cut, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(
            [new Point(size.Width, 0), new Point(size.Width, size.Height - cut), new Point(size.Width - cut, size.Height), new Point(0, size.Height), new Point(0, cut)],
            isStroked: true));
        var geometry = new PathGeometry([figure]);
        geometry.Freeze();
        return geometry;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = RenderSize;
        if (size.Width <= 0)
        {
            return;
        }
        var outline = Outline(size);
        drawingContext.DrawGeometry(PlateFill, null, outline);
        drawingContext.DrawGeometry(TintFill, null, outline);
        var cut = size.Width * IconStyleDesign.HudIconCut;
        drawingContext.DrawLine(ChamferPen, new Point(cut, 0), new Point(0, cut));
        drawingContext.DrawLine(ChamferPen, new Point(size.Width, size.Height - cut), new Point(size.Width - cut, size.Height));
        var edge = new Pen(new SolidColorBrush(ThemeService.WithAlpha(HudDesign.Cyan, 0.28 + 0.5 * Nearness)), 2);
        drawingContext.PushClip(outline);
        drawingContext.DrawGeometry(null, edge, outline);
        drawingContext.Pop();
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

/// <summary>Four corner brackets around the icon (HUD lock-on, SPEC 5.8). <see cref="Inset"/> below zero draws them outside.</summary>
public sealed class HudBrackets : FrameworkElement
{
    public static readonly DependencyProperty InsetProperty = DependencyProperty.Register(
        nameof(Inset), typeof(double), typeof(HudBrackets), new FrameworkPropertyMetadata(-10.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(HudBrackets), new FrameworkPropertyMetadata(new SolidColorBrush(HudDesign.Cyan), FrameworkPropertyMetadataOptions.AffectsRender));

    public HudBrackets()
    {
        IsHitTestVisible = false;
    }

    public double Inset
    {
        get => (double)GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        const double Arm = 8;
        const double Thick = 1.5;
        var r = new Rect(RenderSize);
        r.Inflate(-Inset, -Inset);
        if (r.Width <= 0)
        {
            return;
        }
        foreach (var (x, y, dx, dy) in new[] { (r.Left, r.Top, 1, 1), (r.Right, r.Top, -1, 1), (r.Left, r.Bottom, 1, -1), (r.Right, r.Bottom, -1, -1) })
        {
            drawingContext.DrawRectangle(Stroke, null, new Rect(dx > 0 ? x : x - Arm, dy > 0 ? y : y - Thick, Arm, Thick));
            drawingContext.DrawRectangle(Stroke, null, new Rect(dx > 0 ? x : x - Thick, dy > 0 ? y : y - Arm, Thick, Arm));
        }
    }
}

/// <summary>
/// 1 px cyan lines every 3 px, laid over the HUD hologram (SPEC 5.8). A dozen rectangles per icon, drawn once per size.
/// </summary>
public sealed class HudScanLines : FrameworkElement
{
    private static readonly Brush Line = Frozen(new SolidColorBrush(ThemeService.WithAlpha(HudDesign.Cyan, 0.30)));

    public HudScanLines()
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        for (var y = 0.0; y < RenderSize.Height; y += HudDesign.ScanLinePeriod)
        {
            drawingContext.DrawRectangle(Line, null, new Rect(0, y, RenderSize.Width, 1));
        }
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

/// <summary>Visual helpers shared by the styled icons.</summary>
public static class IconStyleImages
{
    private const int Size = 128;
    private static readonly ConditionalWeakTable<ImageSource, ImageSource> Holograms = [];
    private static readonly ConditionalWeakTable<ImageSource, ImageSource> Monochromes = [];

    /// <summary>
    /// The icon as a cyan hologram (SPEC 5.8, at rest): the mockup's CSS <c>grayscale(1) sepia(.9) hue-rotate(150deg)
    /// saturate(2.6) brightness(1.15)</c>, applied one step at a time like the browser, which clamps after each step.
    /// </summary>
    public static ImageSource Hologram(ImageSource source) =>
        Holograms.GetValue(source, s => Filter(s, (r, g, b, a) =>
        {
            var (fr, fg, fb) = CssFilters.Hologram(r / a, g / a, b / a);
            return (fr * a, fg * a, fb * a);
        }));

    /// <summary>The icon in black and white with more contrast and light (SPEC 5.9, at rest): CSS <c>grayscale(1) contrast(1.5) brightness(1.25)</c>.</summary>
    public static ImageSource Monochrome(ImageSource source) =>
        Monochromes.GetValue(source, s => Filter(s, (r, g, b, a) =>
        {
            var v = CssFilters.Monochrome(r / a, g / a, b / a) * a;
            return (v, v, v);
        }));

    /// <summary>A grid of dots every 4 px whose radius the caller changes (Dot Matrix mask).</summary>
    public static (DrawingBrush Brush, EllipseGeometry Dot) CreateDotMask()
    {
        var dot = new EllipseGeometry(new Point(2, 2), IconStyleDesign.DotRadiusRest, IconStyleDesign.DotRadiusRest);
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, IconStyleDesign.DotPitch, IconStyleDesign.DotPitch))),
                new GeometryDrawing(Brushes.Black, null, dot),
            },
        };
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, IconStyleDesign.DotPitch, IconStyleDesign.DotPitch),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        return (brush, dot);
    }

    /// <summary>Applies <paramref name="pixel"/> to premultiplied channels in 0..1 and returns a frozen 128 px bitmap.</summary>
    private static BitmapSource Filter(ImageSource source, Func<double, double, double, double, (double R, double G, double B)> pixel)
    {
        var bitmap = Rasterize(source);
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var a = pixels[i + 3] / 255.0;
            if (a <= 0)
            {
                continue;
            }
            var (r, g, b) = pixel(pixels[i + 2] / 255.0, pixels[i + 1] / 255.0, pixels[i] / 255.0, a);
            pixels[i] = (byte)Math.Round(b * 255);
            pixels[i + 1] = (byte)Math.Round(g * 255);
            pixels[i + 2] = (byte)Math.Round(r * 255);
        }
        var result = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    private static RenderTargetBitmap Rasterize(ImageSource source)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(source, new Rect(0, 0, Size, Size));
        }
        var target = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return target;
    }
}

/// <summary>Sizes and timings of the styled icons (SPEC 5.8, 5.9).</summary>
public static class IconStyleDesign
{
    /// <summary>HUD plate corner cut, and how far the picture sits inside the plate.</summary>
    public const double HudIconCut = 0.22;
    public const double HudImageInset = 0.21;
    public const double HudImageCorner = 0.24;
    public const double HudBracketsRest = -10;
    public const double HudBracketsLocked = -5;
    public static readonly TimeSpan HudLock = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan HudSweep = TimeSpan.FromMilliseconds(700);
    public static readonly TimeSpan HudBlink = TimeSpan.FromMilliseconds(1100);

    /// <summary>The blinking marker in the edit toolbar: on half the period, off the other half (mockup).</summary>
    public static readonly TimeSpan HudMarkerBlink = TimeSpan.FromMilliseconds(1000);
    public static readonly TimeSpan DotMarkerBlink = TimeSpan.FromMilliseconds(1200);
    public const double HudGlowBase = 3;
    public const double HudGlowGrow = 9;

    /// <summary>
    /// Folder previews: 32% keeps the 2×2 block 14% from the HUD plate's sides, clear of its 22% corner cuts; 30% keeps
    /// the block's corners inside the Dot Matrix circle.
    /// </summary>
    public const double HudFolderPreviewRatio = 0.32;
    public const double DotFolderPreviewRatio = 0.30;

    /// <summary>A corner radius larger than any label, which <see cref="RoundedBorder"/> turns into a capsule.</summary>
    public const double Capsule = 99;

    public const double DotPitch = 4;
    public const double DotRadiusRest = 1.1;
    public const double DotRadiusGrow = 1.95;
    public const double DotRingGap = 6;
    public static readonly TimeSpan DotRingSpin = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan DotRingGrow = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan DotBoot = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// Frame rates of the endless animations. Each icon is a software-rendered window: one hovered ring spinning at
    /// 60 fps kept the app at 0.5% CPU, at 20 fps 0.15%, and a slow spin looks the same. A blink only needs its two changes.
    /// </summary>
    public const int DotRingFrameRate = 20;
    public const int BlinkFrameRate = 10;

    public static readonly TimeSpan HudBootStagger = TimeSpan.FromMilliseconds(45);
    public static readonly TimeSpan DotBootStagger = TimeSpan.FromMilliseconds(40);
}

/// <summary>The dotted ring around a Dot Matrix icon on hover and in edit mode (SPEC 5.9).</summary>
public sealed class DotRing : Shape
{
    public DotRing()
    {
        Stroke = Brushes.White;
        StrokeThickness = 2;
        StrokeDashArray = [0.01, 2.2];
        StrokeDashCap = PenLineCap.Round;
        IsHitTestVisible = false;
        Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 0, BlurRadius = 2, Opacity = 0.6, Color = Colors.Black };
    }

    protected override Geometry DefiningGeometry =>
        new EllipseGeometry(new Rect(StrokeThickness / 2, StrokeThickness / 2, Math.Max(0, ActualWidth - StrokeThickness), Math.Max(0, ActualHeight - StrokeThickness)));
}
