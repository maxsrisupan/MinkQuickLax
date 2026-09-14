using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using MinkQuickLax.Core.Layout;
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

/// <summary>A dashed alignment guide while dragging (SPEC 4.4). One thin window per line keeps redraws cheap.</summary>
public sealed class GuideWindow : OverlayWindow
{
    private readonly Line _line;

    public GuideWindow(bool vertical)
    {
        Vertical = vertical;
        _line = new Line
        {
            StrokeThickness = 1,
            StrokeDashArray = [Motion.GuideDash, Motion.GuideGap],
            SnapsToDevicePixels = true,
            Stretch = Stretch.Fill,
            X2 = vertical ? 0 : 1,
            Y2 = vertical ? 1 : 0,
        };
        _line.SetResourceReference(Shape.StrokeProperty, "Guide");
        Content = _line;
    }

    public bool Vertical { get; }

    public void ShowLine(GuideLine guide, double scale)
    {
        var thickness = Math.Max(1, (int)Math.Round(scale));
        var rect = Vertical
            ? new PixelRect(guide.Position, guide.Start, guide.Position + thickness, guide.End)
            : new PixelRect(guide.Start, guide.Position, guide.End, guide.Position + thickness);
        Width = rect.Width / scale;
        Height = rect.Height / scale;
        if (!IsVisible)
        {
            Show();
        }
        WindowStyles.SetBounds(Handle, rect);
    }
}

/// <summary>The coordinate label next to the pointer while dragging (SPEC 4.4, 5.2).</summary>
public sealed class DragReadoutWindow : OverlayWindow
{
    private readonly TextBlock _text;

    public DragReadoutWindow()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        _text = new TextBlock { FontSize = 11 };
        _text.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
        _text.SetResourceReference(TextBlock.ForegroundProperty, "Skin.Ink");
        var frame = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(8, 3, 8, 3), Child = _text };
        frame.SetResourceReference(Border.BackgroundProperty, "Skin.Fill.Readout");
        frame.SetResourceReference(Border.BorderBrushProperty, "Skin.Edge");
        Content = frame;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
    }

    public void ShowAt(string text, PixelPoint cursor, PixelRect area, double scale)
    {
        _text.Text = text;
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
