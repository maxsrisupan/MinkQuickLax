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
/// focus; transparent margins let clicks pass through.
/// </summary>
public sealed class IconWindow : Window
{
    private readonly Grid _iconHost;
    private readonly Image _image;
    private readonly Border _ripple;
    private readonly ScaleTransform _rippleScale = new(1, 1);
    private readonly Border _missingBadge;
    private readonly Grid _badgeHost;
    private readonly TextBlock _label;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _hop = new();
    private readonly DropShadowEffect _shadow = new() { Direction = 270 };
    private double _iconSize = 48;
    private bool _pressed;
    private double _proximityOpacity = 1;

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
            RenderTransform = new TransformGroup { Children = { _scale, _hop } },
            Background = Brushes.Transparent,
            Children = { _image, _ripple },
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

        _badgeHost = BadgeHost();
        Content = new Grid { Children = { shadowHost, _badgeHost, _label } };
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        _iconHost.MouseLeftButtonDown += OnLeftDown;
        _iconHost.MouseLeftButtonUp += OnLeftUp;
        _iconHost.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            MenuRequested?.Invoke(this);
        };
        _iconHost.MouseEnter += (_, _) => HoverChanged?.Invoke(this, true);
        _iconHost.MouseLeave += (_, _) =>
        {
            _pressed = false;
            HoverChanged?.Invoke(this, false);
        };

        Grid BadgeHost() => new()
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, IconDesign.WindowMargin, 0, 0),
            Children = { _missingBadge },
            IsHitTestVisible = false,
        };
    }

    /// <summary>The icon was clicked (single or double, depending on <see cref="LaunchOnDoubleClick"/>).</summary>
    public event Action<IconWindow>? LaunchRequested;

    public event Action<IconWindow>? MenuRequested;

    public event Action<IconWindow, bool>? HoverChanged;

    public string PlacementId { get; }

    public nint Handle { get; private set; }

    public bool LaunchOnDoubleClick { get; set; }

    public bool IsTargetMissing { get; private set; }

    /// <summary>Target of the proximity animation, updated by <c>ProximityAnimator</c>.</summary>
    public double TargetOpacity { get; set; } = 1;

    public double TargetScale { get; set; } = 1;

    public double CurrentOpacity { get; set; } = 1;

    public double CurrentScale { get; set; } = 1;

    /// <summary>Where the icon itself is, in physical pixels (the window minus its margins and label).</summary>
    public PixelRect IconRect { get; private set; }

    public void SetImage(ImageSource image) => _image.Source = image;

    public void SetAppearance(double iconSizeDip, bool showLabel, string label, bool darkTileShadow)
    {
        _iconSize = iconSizeDip;
        _iconHost.Width = iconSizeDip;
        _iconHost.Height = iconSizeDip;
        _badgeHost.Width = iconSizeDip;
        _badgeHost.Height = iconSizeDip;
        _ripple.CornerRadius = new CornerRadius(iconSizeDip * IconDesign.CornerRatio);

        // SPEC 5.1 TileShadow: light y 6 blur 14 45%; dark y 8 blur 18 70%.
        _shadow.ShadowDepth = darkTileShadow ? 8 : 6;
        _shadow.BlurRadius = darkTileShadow ? 18 : 14;
        _shadow.Opacity = darkTileShadow ? 0.7 : 0.45;
        _shadow.Color = darkTileShadow ? Colors.Black : Color.FromRgb(0x08, 0x1E, 0x1E);

        _label.Text = label;
        _label.Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed;
        _label.Width = iconSizeDip + IconDesign.LabelExtraWidth;
        _label.Margin = new Thickness(0, IconDesign.WindowMargin + iconSizeDip + IconDesign.LabelGap, 0, 0);

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
    public void MoveTo(PixelRect squareRect, int monitorDpi, bool showLabel)
    {
        var scale = monitorDpi / 96.0;
        var height = (int)Math.Round(WindowHeightDip(_iconSize, showLabel) * scale);
        var rect = new PixelRect(squareRect.Left, squareRect.Top, squareRect.Right, squareRect.Top + height);
        WindowStyles.SetBounds(Handle, rect);
        if (WindowStyles.GetDpi(Handle) != monitorDpi)
        {
            // Crossing to a monitor with another scale: WPF resized for the new DPI, so apply the final bounds again.
            WindowStyles.SetBounds(Handle, rect);
        }
        var margin = (int)Math.Round(IconDesign.WindowMargin * scale);
        var icon = (int)Math.Round(_iconSize * scale);
        IconRect = new PixelRect(rect.Left + margin, rect.Top + margin, rect.Left + margin + icon, rect.Top + margin + icon);
    }

    public void SetProximity(double opacity, double scale)
    {
        _proximityOpacity = opacity;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        ApplyOpacity();
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

    /// <summary>Fades and shrinks to 80% (hide) or back (show) after <paramref name="delay"/> (SPEC 4.5, 5.4).</summary>
    public void AnimateVisibility(bool visible, TimeSpan delay, bool reduceMotion)
    {
        if (visible && !IsVisible)
        {
            Opacity = 0;
            Show();
        }
        if (reduceMotion)
        {
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

    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _pressed = true;
        if (LaunchOnDoubleClick && e.ClickCount == 2)
        {
            _pressed = false;
            LaunchRequested?.Invoke(this);
        }
    }

    private void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_pressed && !LaunchOnDoubleClick)
        {
            LaunchRequested?.Invoke(this);
        }
        _pressed = false;
    }
}
