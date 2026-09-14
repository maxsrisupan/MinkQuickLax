using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// Base for floating surfaces: non-layered (so Glass acrylic works), topmost, tool window, never activated.
/// Content sits on a <see cref="SurfaceFrame"/> drawn in the current style (SPEC 5.2, 5.8, 5.9).
/// </summary>
public abstract class SurfaceWindow : Window
{
    private readonly ThemeService _theme;
    private readonly SurfaceFrame _frame;
    private readonly Grid _root;
    private readonly ScaleTransform _scale = new(1, 1);

    protected SurfaceWindow(ThemeService theme, string fillResourceKey)
    {
        _theme = theme;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            GlassFrameThickness = new Thickness(-1),
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        _frame = new SurfaceFrame { RenderTransform = _scale };
        _frame.SetResourceReference(SurfaceFrame.FillProperty, fillResourceKey);
        _root = new Grid { Children = { _frame } };
        base.Content = _root;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        _theme.Changed += OnLookChanged;
        Closed += (_, _) => _theme.Changed -= OnLookChanged;
    }

    public nint Handle { get; private set; }

    /// <summary>The content inside the glass frame.</summary>
    protected UIElement? Body
    {
        get => _frame.Child;
        set => _frame.Child = value;
    }

    protected Thickness BodyPadding
    {
        get => _frame.Padding;
        set => _frame.Padding = value;
    }

    /// <summary>Transparent space around the plate, for decorations drawn outside it (see <see cref="Root"/>).</summary>
    protected Thickness FrameMargin
    {
        get => _frame.Margin;
        set => _frame.Margin = value;
    }

    /// <summary>The grid holding the plate; extra children are drawn over the window, outside or on the plate.</summary>
    protected Grid Root => _root;

    protected Look CurrentLook => _theme.Current;

    protected SurfaceShape Shape
    {
        get => _frame.Shape;
        set => _frame.Shape = value;
    }

    protected virtual SurfaceBehavior Behavior => SurfaceBehavior.NoActivate;

    public PixelRect Bounds => WindowStyles.GetBounds(Handle);

    /// <summary>Shows the window off screen to measure it, then lets <paramref name="place"/> pick the top-left corner.</summary>
    public void ShowPlaced(Func<PixelSize, PixelPoint> place, bool animate)
    {
        Left = -32000;
        Top = -32000;
        Show();
        UpdateLayout();
        var bounds = WindowStyles.GetBounds(Handle);
        var topLeft = place(bounds.Size);
        WindowStyles.SetBounds(Handle, new PixelRect(topLeft.X, topLeft.Y, topLeft.X + bounds.Width, topLeft.Y + bounds.Height));
        if (animate && !_theme.Current.ReduceMotion)
        {
            AnimateIn();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyFloating(Handle, Behavior);
        if (HwndSource.FromHwnd(Handle) is { } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
        ApplyLook(_theme.Current);
    }

    protected virtual void AnimateIn()
    {
        _frame.RenderTransformOrigin = new Point(0.5, 0);
        var fade = new DoubleAnimation(0, 1, Motion.MenuOpen) { EasingFunction = Motion.Out };
        var grow = new DoubleAnimation(Motion.MenuStartScale, 1, Motion.MenuOpen) { EasingFunction = Motion.Out };
        _frame.BeginAnimation(OpacityProperty, fade);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>The style or theme changed while the window is open.</summary>
    protected virtual void OnLookChanged(Look look)
    {
        if (Handle != 0)
        {
            ApplyLook(look);
        }
    }

    private void ApplyLook(Look look)
    {
        Skin.SetKind(this, look.Style);
        DwmBackdrop.SetDarkFrame(Handle, look.SurfacesDark);
        // Only Glass keeps the system's rounded corners and blur; the other styles draw their own shape (SPEC 5.5).
        DwmBackdrop.SetRoundedCorners(Handle, rounded: look.Style == Core.Model.StyleSetting.Glass);
        if (!look.Blur || !DwmBackdrop.SetAcrylic(Handle, true))
        {
            DwmBackdrop.SetAcrylic(Handle, false);
        }
    }

    /// <summary>Top-left corner for a surface of <paramref name="size"/> opening at <paramref name="anchor"/>, flipped to stay inside <paramref name="area"/>.</summary>
    public static PixelPoint PlaceAtPointer(PixelPoint anchor, PixelSize size, PixelRect area)
    {
        var x = anchor.X + size.Width <= area.Right ? anchor.X : anchor.X - size.Width;
        var y = anchor.Y + size.Height <= area.Bottom ? anchor.Y : anchor.Y - size.Height;
        var rect = new PixelRect(x, y, x + size.Width, y + size.Height).MoveInside(area);
        return new PixelPoint(rect.Left, rect.Top);
    }
}
