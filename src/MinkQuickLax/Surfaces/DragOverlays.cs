using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <summary>Base for small layered overlays that never take clicks or focus.</summary>
public abstract class OverlayWindow : Window
{
    protected OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;
        Left = -32000;
        Top = -32000;
    }

    public nint Handle { get; private set; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyFloating(Handle, SurfaceBehavior.NoActivate | SurfaceBehavior.ClickThrough);
        if (HwndSource.FromHwnd(Handle) is { } source)
        {
            source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
        }
    }
}

/// <summary>
/// An alignment guide while dragging (SPEC 4.4): dashes 6/5 in Glass, cyan dashes 4/4 in HUD, red dots of radius 1.2
/// every 6 px in Dot Matrix (SPEC 5.8, 5.9). One thin window per line keeps redraws cheap.
/// </summary>
public sealed class GuideWindow : OverlayWindow
{
    private const double DotRadius = 1.2;
    private const double DotPitch = 6;

    private readonly GuideStroke _stroke;

    public GuideWindow(bool vertical)
    {
        Vertical = vertical;
        _stroke = new GuideStroke { Vertical = vertical, SnapsToDevicePixels = true };
        _stroke.SetResourceReference(GuideStroke.BrushProperty, "Guide");
        Content = _stroke;
    }

    public bool Vertical { get; }

    public void ShowLine(GuideLine guide, double scale, StyleSetting style)
    {
        _stroke.Style = style;
        var thickness = style == StyleSetting.Dot ? (int)Math.Ceiling(2 * DotRadius * scale) : Math.Max(1, (int)Math.Round(scale));
        var start = guide.Position - (thickness - Math.Max(1, (int)Math.Round(scale))) / 2;
        var rect = Vertical
            ? new PixelRect(start, guide.Start, start + thickness, guide.End)
            : new PixelRect(guide.Start, start, guide.End, start + thickness);
        Width = rect.Width / scale;
        Height = rect.Height / scale;
        if (!IsVisible)
        {
            Show();
        }
        WindowStyles.SetBounds(Handle, rect);
    }

    /// <summary>Draws the guide itself; <see cref="Style"/> picks dashes or dots.</summary>
    private sealed class GuideStroke : FrameworkElement
    {
        public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
            nameof(Brush), typeof(Brush), typeof(GuideStroke), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        private StyleSetting _style;

        public bool Vertical { get; init; }

        public Brush? Brush
        {
            get => (Brush?)GetValue(BrushProperty);
            set => SetValue(BrushProperty, value);
        }

        public new StyleSetting Style
        {
            get => _style;
            set
            {
                if (_style != value)
                {
                    _style = value;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (Brush is not { } brush)
            {
                return;
            }
            var length = Vertical ? RenderSize.Height : RenderSize.Width;
            var across = Vertical ? RenderSize.Width : RenderSize.Height;
            if (_style == StyleSetting.Dot)
            {
                for (var at = DotRadius; at <= length - DotRadius; at += DotPitch)
                {
                    var center = Vertical ? new Point(across / 2, at) : new Point(at, across / 2);
                    drawingContext.DrawEllipse(brush, null, center, DotRadius, DotRadius);
                }
                return;
            }
            var (dash, gap) = _style == StyleSetting.Hud ? (4.0, 4.0) : (Motion.GuideDash, Motion.GuideGap);
            for (var at = 0.0; at < length; at += dash + gap)
            {
                var run = Math.Min(dash, length - at);
                drawingContext.DrawRectangle(brush, null, Vertical ? new Rect(0, at, across, run) : new Rect(at, 0, run, across));
            }
        }
    }
}

/// <summary>
/// The coordinate label next to the pointer while dragging (SPEC 4.4, 5.2): rounded in Glass, square with cyan text in
/// HUD, a capsule in Doto in Dot Matrix (SPEC 5.8, 5.9).
/// </summary>
public sealed class DragReadoutWindow : OverlayWindow
{
    private readonly TextBlock _text;
    private readonly Border _frame;

    public DragReadoutWindow()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        _text = new TextBlock { FontSize = 11 };
        _text.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Readout");
        _frame = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(8, 3, 8, 3), Child = _text };
        _frame.SetResourceReference(Border.BackgroundProperty, "Skin.Fill.Readout");
        _frame.SetResourceReference(Border.BorderBrushProperty, "Skin.Edge");
        Content = _frame;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
    }

    public void ShowAt(string text, PixelPoint cursor, PixelRect area, double scale, StyleSetting style)
    {
        _text.Text = text;
        _text.FontSize = style == StyleSetting.Dot ? 13 : 11;
        _text.FontWeight = style == StyleSetting.Dot ? FontWeights.ExtraBold : FontWeights.Normal;
        _text.SetResourceReference(TextBlock.ForegroundProperty, style == StyleSetting.Hud ? "Accent" : "Skin.Ink");
        _frame.CornerRadius = new CornerRadius(style switch { StyleSetting.Hud => 0, StyleSetting.Dot => 99, _ => 8 });
        _frame.Padding = style == StyleSetting.Dot ? new Thickness(10, 1, 10, 2) : new Thickness(8, 3, 8, 3);
        if (!IsVisible)
        {
            Show();
        }
        UpdateLayout();
        var offset = (int)Math.Round(Motion.ReadoutOffset * scale);
        var bounds = WindowStyles.GetBounds(Handle);
        var x = cursor.X + offset;
        var y = cursor.Y + offset;
        var rect = new PixelRect(x, y, x + bounds.Width, y + bounds.Height).MoveInside(area);
        WindowStyles.SetPosition(Handle, rect.Left, rect.Top);
    }
}
