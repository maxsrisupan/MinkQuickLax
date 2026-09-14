using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Styles.Glass;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// One floating icon (SPEC 4.2, 5.3). A layered, software-rendered, topmost tool window that never takes
/// focus; transparent margins let clicks pass through. Mouse input is reported to a controller, which
/// decides between launching, selecting and dragging.
/// </summary>
public sealed class IconWindow : Window
{
    private readonly Grid _iconHost;
    private readonly Image _image;
    private readonly Border _ripple;
    private readonly ScaleTransform _rippleScale = new(1, 1);
    private readonly Border _selection;
    private readonly Border _missingBadge;
    private readonly Grid _badgeHost;
    private readonly TextBlock _label;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly ScaleTransform _lift = new(1, 1);
    private readonly RotateTransform _jiggle = new();
    private readonly TranslateTransform _hop = new();
    private readonly DropShadowEffect _shadow = new() { Direction = 270 };
    private double _iconSize = 48;
    private bool _showLabel;
    private double _proximityOpacity = 1;
    private double _restShadowOpacity;

    public IconWindow(string placementId)
    {
        PlacementId = placementId;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        UseLayoutRounding = true;
        Left = -32000;
        Top = -32000;

        _image = new Image { Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _ripple = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(2), Opacity = 0, RenderTransform = _rippleScale, RenderTransformOrigin = new Point(0.5, 0.5), IsHitTestVisible = false };
        _selection = new Border { BorderThickness = new Thickness(2), Margin = new Thickness(-5), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        _selection.SetResourceReference(Border.BorderBrushProperty, "Accent");
        _missingBadge = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -4, -4, 0),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Child = new TextBlock { Text = "!", FontWeight = FontWeights.Bold, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        _missingBadge.SetResourceReference(Border.BackgroundProperty, "Danger");

        _iconHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, IconDesign.WindowMargin, 0, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { _scale, _lift, _jiggle, _hop } },
            Background = Brushes.Transparent,
            Children = { _image, _ripple, _selection },
        };
        var shadowHost = new Grid { Effect = _shadow, Children = { _iconHost } };

        _label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = IconDesign.LabelFontSize,
            Foreground = Brushes.White,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { Direction = 270, ShadowDepth = 1, BlurRadius = 3, Opacity = 0.75, Color = Colors.Black },
        };
        _label.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Ui");

        _badgeHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, IconDesign.WindowMargin, 0, 0),
            Children = { _missingBadge },
            IsHitTestVisible = false,
        };
        Content = new Grid { Children = { shadowHost, _badgeHost, _label } };
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        _iconHost.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Pressed?.Invoke(this, e.ClickCount);
        };
        _iconHost.MouseMove += (_, _) => PointerMoved?.Invoke(this);
        _iconHost.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Released?.Invoke(this);
        };
        _iconHost.LostMouseCapture += (_, _) => CaptureLost?.Invoke(this);
        _iconHost.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            MenuRequested?.Invoke(this);
        };
        _iconHost.MouseEnter += (_, _) => HoverChanged?.Invoke(this, true);
        _iconHost.MouseLeave += (_, _) => HoverChanged?.Invoke(this, false);
    }

    /// <summary>Left button went down on the icon; the argument is the click count (2 for a double click).</summary>
    public event Action<IconWindow, int>? Pressed;

    public event Action<IconWindow>? PointerMoved;

    public event Action<IconWindow>? Released;

    public event Action<IconWindow>? CaptureLost;

    public event Action<IconWindow>? MenuRequested;

    public event Action<IconWindow, bool>? HoverChanged;

    public string PlacementId { get; }

    public nint Handle { get; private set; }

    public bool IsTargetMissing { get; private set; }

    /// <summary>Target of the proximity animation, updated by <c>ProximityAnimator</c>.</summary>
    public double TargetOpacity { get; set; } = 1;

    public double TargetScale { get; set; } = 1;

    public double CurrentOpacity { get; set; } = 1;

    public double CurrentScale { get; set; } = 1;

    /// <summary>Where the icon itself is, in physical pixels (the window minus its margins and label).</summary>
    public PixelRect IconRect { get; private set; }

    /// <summary>The square icon area plus its margins: what snapping and collisions use.</summary>
    public PixelRect SquareRect { get; private set; }

    public void SetImage(ImageSource image) => _image.Source = image;

    public void SetAppearance(double iconSizeDip, bool showLabel, string label, bool darkTileShadow)
    {
        _iconSize = iconSizeDip;
        _showLabel = showLabel;
        _iconHost.Width = iconSizeDip;
        _iconHost.Height = iconSizeDip;
        _badgeHost.Width = iconSizeDip;
        _badgeHost.Height = iconSizeDip;
        var corner = iconSizeDip * IconDesign.CornerRatio;
        _ripple.CornerRadius = new CornerRadius(corner);
        _selection.CornerRadius = new CornerRadius(corner + 4);

        // SPEC 5.1 TileShadow: light y 6 blur 14 45%; dark y 8 blur 18 70%.
        _shadow.ShadowDepth = darkTileShadow ? 8 : 6;
        _shadow.BlurRadius = darkTileShadow ? 18 : 14;
        _restShadowOpacity = darkTileShadow ? 0.7 : 0.45;
        _shadow.Opacity = _restShadowOpacity;
        _shadow.Color = darkTileShadow ? Colors.Black : Color.FromRgb(0x08, 0x1E, 0x1E);

        _label.Text = label;
        _label.Width = iconSizeDip + IconDesign.LabelExtraWidth;
        _label.Margin = new Thickness(0, IconDesign.WindowMargin + iconSizeDip + IconDesign.LabelGap, 0, 0);
        _label.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;

        Width = iconSizeDip + 2 * IconDesign.WindowMargin;
        Height = WindowHeightDip(iconSizeDip, showLabel);
    }

    public static double WindowHeightDip(double iconSizeDip, bool showLabel) =>
        showLabel
            ? IconDesign.WindowMargin + iconSizeDip + IconDesign.LabelGap + IconDesign.LabelHeight + 4
            : iconSizeDip + 2 * IconDesign.WindowMargin;

    public void SetMissing(bool missing)
    {
        IsTargetMissing = missing;
        _missingBadge.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        ApplyOpacity();
    }

    /// <summary>Places the window so the square icon area is <paramref name="squareRect"/> (physical pixels).</summary>
    public void MoveTo(PixelRect squareRect, int monitorDpi)
    {
        var scale = monitorDpi / 96.0;
        var height = (int)Math.Round(WindowHeightDip(_iconSize, _showLabel) * scale);
        var rect = new PixelRect(squareRect.Left, squareRect.Top, squareRect.Right, squareRect.Top + height);
        WindowStyles.SetBounds(Handle, rect);
        if (WindowStyles.GetDpi(Handle) != monitorDpi)
        {
            // Crossing to a monitor with another scale: WPF resized for the new DPI, so apply the final bounds again.
            WindowStyles.SetBounds(Handle, rect);
        }
        var margin = (int)Math.Round(IconDesign.WindowMargin * scale);
        var icon = (int)Math.Round(_iconSize * scale);
        SquareRect = squareRect;
        IconRect = new PixelRect(rect.Left + margin, rect.Top + margin, rect.Left + margin + icon, rect.Top + margin + icon);
    }

    public bool BeginCapture() => _iconHost.CaptureMouse();

    public void EndCapture() => _iconHost.ReleaseMouseCapture();

    public void SetProximity(double opacity, double scale)
    {
        _proximityOpacity = opacity;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        ApplyOpacity();
    }

    /// <summary>Edit mode: tilt back and forth, each icon starting at a different point (SPEC 5.4). Labels hide.</summary>
    public void SetArranging(bool arranging, double phase, bool reduceMotion)
    {
        _label.Visibility = !arranging && _showLabel ? Visibility.Visible : Visibility.Collapsed;
        if (!arranging || reduceMotion)
        {
            _jiggle.BeginAnimation(RotateTransform.AngleProperty, null);
            _jiggle.Angle = 0;
            return;
        }
        var wobble = new DoubleAnimation(-Motion.JiggleAngle, Motion.JiggleAngle, Motion.Jiggle)
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        var clock = wobble.CreateClock();
        clock.Controller!.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(Motion.Jiggle.TotalMilliseconds * 2 * phase), TimeSeekOrigin.BeginTime);
        _jiggle.ApplyAnimationClock(RotateTransform.AngleProperty, clock);
    }

    public void SetSelected(bool selected) =>
        _selection.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>While dragging: 1.12× and a deeper shadow (SPEC 5.4).</summary>
    public void SetLifted(bool lifted)
    {
        var scale = lifted ? Motion.DragScale : 1;
        _lift.ScaleX = scale;
        _lift.ScaleY = scale;
        _shadow.Opacity = lifted ? Math.Min(1, _restShadowOpacity + 0.2) : _restShadowOpacity;
        _jiggle.Angle = 0;
    }

    public void PlayLaunch(bool reduceMotion)
    {
        if (reduceMotion)
        {
            return;
        }
        // Hop: up 16, down, up 6, down (SPEC 5.4 / mockup keyframes 20/45/65/85%).
        var hop = new DoubleAnimationUsingKeyFrames { Duration = Motion.LaunchHop };
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(-16, KeyTime.FromPercent(0.20), Motion.Out));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.45), Motion.Out));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(-6, KeyTime.FromPercent(0.65), Motion.Out));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.85), Motion.Out));
        _hop.BeginAnimation(TranslateTransform.YProperty, hop);

        var grow = new DoubleAnimation(1, 1.7, Motion.LaunchRipple) { EasingFunction = Motion.Out };
        _rippleScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _rippleScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        _ripple.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0, Motion.LaunchRipple) { EasingFunction = Motion.Out });
    }

    /// <summary>Fades out (hide) or in (show) after <paramref name="delay"/> (SPEC 4.5, 5.4).</summary>
    public void AnimateVisibility(bool visible, TimeSpan delay, bool reduceMotion)
    {
        if (visible && !IsVisible)
        {
            Opacity = 0;
            Show();
        }
        if (reduceMotion)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = visible ? 1 : 0;
            if (!visible)
            {
                Hide();
            }
            return;
        }
        var fade = new DoubleAnimation(visible ? 1 : 0, Motion.HideShow) { BeginTime = delay, EasingFunction = Motion.Out };
        if (!visible)
        {
            fade.Completed += (_, _) =>
            {
                if (Opacity == 0)
                {
                    Hide();
                }
            };
        }
        BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        WindowStyles.ApplyFloating(Handle, SurfaceBehavior.NoActivate);
        // Software rendering: ~40 MB less memory for 30 icons and no extra CPU (spike S1/S3).
        if (HwndSource.FromHwnd(Handle) is { } source)
        {
            source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
        }
    }

    private void ApplyOpacity() =>
        _iconHost.Opacity = _proximityOpacity * (IsTargetMissing ? IconDesign.MissingOpacity : 1);
}
