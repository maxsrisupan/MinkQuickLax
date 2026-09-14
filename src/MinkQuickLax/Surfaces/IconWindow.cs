using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// One floating icon (SPEC 4.2, 5.3, 5.8, 5.9). A layered, software-rendered, topmost tool window that never takes
/// focus; transparent margins let clicks pass through. It draws itself in the current style — Glass tile, HUD
/// hologram plate or Dot Matrix disc — and reports mouse input to a controller, which decides between launching,
/// selecting and dragging.
/// </summary>
public sealed class IconWindow : Window
{
    /// <summary>Extra nearness while the "style changed" pass plays (Dot Matrix dots start full and shrink back).</summary>
    private static readonly DependencyProperty BootNearnessProperty = DependencyProperty.Register(
        "BootNearness", typeof(double), typeof(IconWindow), new PropertyMetadata(0.0, (d, _) => ((IconWindow)d).ApplyNearness()));

    private static readonly Brush HitFill = CreateHitFill();

    private readonly Grid _iconHost;
    private readonly Grid _visuals;
    private readonly Grid _shadowHost;
    private readonly DropShadowEffect _shadow = new() { Direction = 270 };

    // The picture: the icon, its styled twin (hologram or monochrome) and HUD scan lines.
    private readonly Grid _picture;
    private readonly Image _image;
    private readonly Image _styledImage;
    private readonly HudScanLines _scanLines;
    private readonly DrawingBrush _dotMask;
    private readonly EllipseGeometry _dotShape;

    private readonly HudPlate _hudPlate;
    private readonly DropShadowEffect _hudGlow = new() { ShadowDepth = 0, Color = HudDesign.Cyan };
    private readonly Canvas _sweepHost;
    private readonly Rectangle _sweep;
    private readonly TranslateTransform _sweepMove = new();
    private readonly HudBrackets _brackets;
    private readonly DotRing _ring;
    private readonly RotateTransform _ringSpin = new();
    private readonly ScaleTransform _ringGrow = new(1, 1);

    private readonly Border _folderSheet;
    private readonly UniformGrid _folderGrid;
    private readonly List<(Image Original, Image Styled)> _folderCells = [];

    private readonly Path _ripple;
    private readonly ScaleTransform _rippleScale = new(1, 1);
    private readonly Path _selection;
    private readonly Border _missingBadge;
    private readonly Grid _badgeHost;
    private readonly Border _labelFrame;
    private readonly TextBlock _label;
    private readonly DropShadowEffect _labelShadow = new() { Direction = 270, ShadowDepth = 1, BlurRadius = 3, Opacity = 0.75, Color = Colors.Black };

    private readonly ScaleTransform _scale = new(1, 1);
    private readonly ScaleTransform _lift = new(1, 1);
    private readonly RotateTransform _jiggle = new();
    private readonly TranslateTransform _hop = new();

    private double _iconSize = 48;
    private bool _showLabel;
    private string _labelText = "";
    private double _proximityOpacity = 1;
    private double _nearness;
    private double _restShadowOpacity;
    private bool _arranging;
    private bool _reduceMotion;
    private StyleSetting _style = StyleSetting.Glass;
    private ImageSource? _source;
    private IReadOnlyList<ImageSource?> _previews = [];

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
        _styledImage = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        RenderOptions.SetBitmapScalingMode(_styledImage, BitmapScalingMode.HighQuality);
        _scanLines = new HudScanLines { Visibility = Visibility.Collapsed };
        (_dotMask, _dotShape) = IconStyleImages.CreateDotMask();
        _picture = new Grid { Children = { _image, _styledImage, _scanLines } };

        _hudPlate = new HudPlate { Visibility = Visibility.Collapsed, Effect = _hudGlow, IsHitTestVisible = false };
        _sweep = new Rectangle
        {
            Fill = new LinearGradientBrush(
                [
                    new GradientStop(ThemeService.WithAlpha(HudDesign.Cyan, 0), 0.42),
                    new GradientStop(ThemeService.WithAlpha(HudDesign.Cyan, 0.85), 0.5),
                    new GradientStop(ThemeService.WithAlpha(HudDesign.Cyan, 0), 0.58),
                ],
                new Point(0, 0), new Point(0, 1)),
            RenderTransform = _sweepMove,
            Opacity = 0,
        };
        _sweepHost = new Canvas { Visibility = Visibility.Collapsed, IsHitTestVisible = false, Children = { _sweep } };
        _brackets = new HudBrackets { Opacity = 0, Visibility = Visibility.Collapsed };
        _ring = new DotRing
        {
            Opacity = 0,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(-IconStyleDesign.DotRingGap),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { _ringGrow, _ringSpin } },
        };

        // Folder (SPEC 4.3, 5.2): a sheet with 2×2 previews, each 36% wide with 8% between them.
        _folderGrid = new UniformGrid { Rows = 2, Columns = 2 };
        _folderSheet = new Border { BorderThickness = new Thickness(1), Child = _folderGrid, Visibility = Visibility.Collapsed };

        _ripple = new Path { Stroke = Brushes.White, StrokeThickness = 2, Stretch = Stretch.Fill, Opacity = 0, RenderTransform = _rippleScale, RenderTransformOrigin = new Point(0.5, 0.5), IsHitTestVisible = false };
        _selection = new Path { StrokeThickness = 2, Stretch = Stretch.Fill, Margin = new Thickness(-5), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        _selection.SetResourceReference(Shape.StrokeProperty, "Accent");

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

        _visuals = new Grid { Children = { _hudPlate, _picture, _folderSheet, _sweepHost, _ripple, _selection, _brackets, _ring } };
        _iconHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, IconDesign.WindowMargin, 0, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { _scale, _lift, _jiggle, _hop } },
            // Layered windows let clicks through fully transparent pixels; alpha 1 keeps the gaps between Dot Matrix
            // dots and the transparent parts of icons clickable without being visible. The fading happens on the layers
            // inside, so this fill never fades to zero.
            Background = HitFill,
            Children = { _visuals },
        };
        _shadowHost = new Grid { Effect = _shadow, Children = { _iconHost } };

        _label = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = IconDesign.LabelFontSize,
            Foreground = Brushes.White,
        };
        _labelFrame = new Border
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Child = _label,
        };

        _badgeHost = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, IconDesign.WindowMargin, 0, 0),
            Children = { _missingBadge },
            IsHitTestVisible = false,
        };
        Content = new Grid { Children = { _shadowHost, _badgeHost, _labelFrame } };
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
        _iconHost.MouseEnter += (_, _) =>
        {
            ShowHover(true);
            HoverChanged?.Invoke(this, true);
        };
        _iconHost.MouseLeave += (_, _) =>
        {
            ShowHover(false);
            HoverChanged?.Invoke(this, false);
        };
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

    public double TargetNearness { get; set; }

    public double CurrentOpacity { get; set; } = 1;

    public double CurrentScale { get; set; } = 1;

    public double CurrentNearness { get; set; }

    /// <summary>Where the icon itself is, in physical pixels (the window minus its margins and label).</summary>
    public PixelRect IconRect { get; private set; }

    /// <summary>The square icon area plus its margins: what snapping and collisions use.</summary>
    public PixelRect SquareRect { get; private set; }

    /// <summary>A group's folder rather than a single link.</summary>
    public bool IsFolder => _folderSheet.Visibility == Visibility.Visible;

    public void SetImage(ImageSource image)
    {
        _source = image;
        _image.Source = image;
        _picture.Visibility = Visibility.Visible;
        _folderSheet.Visibility = Visibility.Collapsed;
        RefreshStyledImages();
    }

    /// <summary>Shows a folder with up to four previews; an empty group shows an empty folder (SPEC 4.3).</summary>
    public void SetFolder(IReadOnlyList<ImageSource?> previews)
    {
        _previews = previews;
        _source = null;
        _picture.Visibility = Visibility.Collapsed;
        _folderSheet.Visibility = Visibility.Visible;
        _folderGrid.Children.Clear();
        _folderCells.Clear();
        for (var i = 0; i < 4; i++)
        {
            var original = new Image { Stretch = Stretch.Uniform, Source = i < previews.Count ? previews[i] : null };
            var styled = new Image { Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(original, BitmapScalingMode.HighQuality);
            RenderOptions.SetBitmapScalingMode(styled, BitmapScalingMode.HighQuality);
            _folderCells.Add((original, styled));
            _folderGrid.Children.Add(new Grid { Margin = new Thickness(_iconSize * IconDesign.FolderGapRatio / 2), Children = { original, styled } });
        }
        ApplyStyleLayout();
        RefreshStyledImages();
    }

    /// <summary>Something dragged over this icon will join it or create a group: grow as the signal (SPEC 4.4).</summary>
    public void SetDropTarget(bool active)
    {
        var scale = active ? Motion.DragScale : 1;
        _lift.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(160)) { EasingFunction = Motion.Out });
        _lift.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(scale, TimeSpan.FromMilliseconds(160)) { EasingFunction = Motion.Out });
        _selection.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetAppearance(double iconSizeDip, bool showLabel, string label, Look look)
    {
        _iconSize = iconSizeDip;
        _showLabel = showLabel;
        _labelText = label;
        _style = look.Style;
        _reduceMotion = look.ReduceMotion;
        _iconHost.Width = iconSizeDip;
        _iconHost.Height = iconSizeDip;
        _badgeHost.Width = iconSizeDip;
        _badgeHost.Height = iconSizeDip;

        // SPEC 5.1 TileShadow (Glass only): light y 6 blur 14 45%; dark y 8 blur 18 70%.
        _shadow.ShadowDepth = look.Dark ? 8 : 6;
        _shadow.BlurRadius = look.Dark ? 18 : 14;
        _restShadowOpacity = look.Dark ? 0.7 : 0.45;
        _shadow.Opacity = _restShadowOpacity;
        _shadow.Color = look.Dark ? Colors.Black : Color.FromRgb(0x08, 0x1E, 0x1E);

        ApplyStyleLayout();
        ApplyLabel(look);
        RefreshStyledImages();

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

    /// <param name="nearness">0 at rest, 1 when the mouse is on the icon: drives the HUD hologram and the Dot Matrix dots.</param>
    public void SetProximity(double opacity, double scale, double nearness)
    {
        _proximityOpacity = opacity;
        _nearness = nearness;
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        ApplyOpacity();
        ApplyNearness();
    }

    /// <summary>
    /// Edit mode (SPEC 4.4, 5.8, 5.9): Glass icons tilt back and forth, each starting at a different point; HUD shows
    /// blinking amber brackets; Dot Matrix shows its spinning dotted ring. Labels hide and every icon shows full color.
    /// </summary>
    public void SetArranging(bool arranging, double phase, bool reduceMotion)
    {
        _arranging = arranging;
        _reduceMotion = reduceMotion;
        _labelFrame.Visibility = !arranging && _showLabel ? Visibility.Visible : Visibility.Collapsed;
        _jiggle.BeginAnimation(RotateTransform.AngleProperty, null);
        _jiggle.Angle = 0;
        ApplyNearness();

        if (_style == StyleSetting.Hud)
        {
            _brackets.Stroke = new SolidColorBrush(arranging ? HudDesign.Amber : HudDesign.Cyan);
            _brackets.BeginAnimation(OpacityProperty, null);
            _brackets.BeginAnimation(HudBrackets.InsetProperty, null);
            if (arranging)
            {
                _brackets.Inset = IconStyleDesign.HudBracketsLocked;
                if (reduceMotion)
                {
                    _brackets.Opacity = 1;
                }
                else
                {
                    var blink = new DoubleAnimationUsingKeyFrames { Duration = IconStyleDesign.HudBlink, RepeatBehavior = RepeatBehavior.Forever };
                    Timeline.SetDesiredFrameRate(blink, IconStyleDesign.BlinkFrameRate);
                    blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
                    blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, KeyTime.FromPercent(0.5)));
                    _brackets.BeginAnimation(OpacityProperty, blink);
                }
            }
            else
            {
                _brackets.Inset = _iconHost.IsMouseOver ? IconStyleDesign.HudBracketsLocked : IconStyleDesign.HudBracketsRest;
                _brackets.Opacity = _iconHost.IsMouseOver ? 1 : 0;
            }
            return;
        }
        if (_style == StyleSetting.Dot)
        {
            ShowRing(arranging || _iconHost.IsMouseOver);
            return;
        }
        if (!arranging || reduceMotion)
        {
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
        _lift.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _lift.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _lift.ScaleX = scale;
        _lift.ScaleY = scale;
        _shadow.Opacity = lifted ? Math.Min(1, _restShadowOpacity + 0.2) : _restShadowOpacity;
        _jiggle.BeginAnimation(RotateTransform.AngleProperty, null);
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
        if (_style == StyleSetting.Hud)
        {
            PlaySweep(TimeSpan.Zero);
        }
    }

    /// <summary>A new icon springs in (SPEC 5.4: 600 ms, 90 ms apart).</summary>
    public void PlayPopIn(TimeSpan delay, bool reduceMotion)
    {
        if (reduceMotion)
        {
            return;
        }
        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { BeginTime = delay });
        var grow = new DoubleAnimation(0.4, 1, Motion.AddPop) { BeginTime = delay, EasingFunction = Motion.Spring };
        _lift.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _lift.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>The pass that plays when the user switches to this style (SPEC 5.8, 5.9), after <paramref name="delay"/>.</summary>
    public void PlayStyleIntro(TimeSpan delay)
    {
        if (_reduceMotion)
        {
            return;
        }
        switch (_style)
        {
            case StyleSetting.Hud:
                PlaySweep(delay);
                if (!_arranging)
                {
                    var lockOn = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = TimeSpan.FromMilliseconds(600), FillBehavior = FillBehavior.Stop };
                    lockOn.KeyFrames.Add(new DiscreteDoubleKeyFrame(-14, KeyTime.FromPercent(0)));
                    lockOn.KeyFrames.Add(new EasingDoubleKeyFrame(IconStyleDesign.HudBracketsLocked, KeyTime.FromPercent(0.6), Motion.Out));
                    var flash = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = TimeSpan.FromMilliseconds(600), FillBehavior = FillBehavior.Stop };
                    flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
                    flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0.6)));
                    flash.KeyFrames.Add(new LinearDoubleKeyFrame(_iconHost.IsMouseOver ? 1 : 0, KeyTime.FromPercent(1)));
                    _brackets.BeginAnimation(HudBrackets.InsetProperty, lockOn);
                    _brackets.BeginAnimation(OpacityProperty, flash);
                }
                break;
            case StyleSetting.Dot:
                var shrink = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = IconStyleDesign.DotBoot, FillBehavior = FillBehavior.Stop };
                shrink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
                shrink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0.35)));
                shrink.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), Motion.Out));
                BeginAnimation(BootNearnessProperty, shrink);
                break;
            default:
                var fade = new DoubleAnimation(0.2, 1, Motion.HideShow) { BeginTime = delay, EasingFunction = Motion.Out, FillBehavior = FillBehavior.Stop };
                var grow = new DoubleAnimation(Motion.HiddenScale, 1, Motion.AddPop) { BeginTime = delay, EasingFunction = Motion.Spring };
                _shadowHost.BeginAnimation(OpacityProperty, fade);
                _lift.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
                _lift.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                break;
        }
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

    /// <summary>Shapes, clips and which layers show for the current style and size.</summary>
    private void ApplyStyleLayout()
    {
        var size = new Size(_iconSize, _iconSize);
        var hud = _style == StyleSetting.Hud;
        var dot = _style == StyleSetting.Dot;

        _shadowHost.Effect = _style == StyleSetting.Glass ? _shadow : null;
        _hudPlate.Visibility = hud ? Visibility.Visible : Visibility.Collapsed;
        _sweepHost.Visibility = hud ? Visibility.Visible : Visibility.Collapsed;
        _brackets.Visibility = hud ? Visibility.Visible : Visibility.Collapsed;
        _ring.Visibility = dot ? Visibility.Visible : Visibility.Collapsed;
        _scanLines.Visibility = hud ? Visibility.Visible : Visibility.Collapsed;
        _styledImage.Visibility = hud || dot ? Visibility.Visible : Visibility.Collapsed;

        // The picture: full tile in Glass, inside the plate in HUD, a dotted disc in Dot Matrix.
        if (hud)
        {
            var inset = _iconSize * IconStyleDesign.HudImageInset;
            var inner = _iconSize - 2 * inset;
            _picture.Margin = new Thickness(inset);
            _picture.Clip = new RectangleGeometry(new Rect(0, 0, inner, inner), inner * IconStyleDesign.HudImageCorner, inner * IconStyleDesign.HudImageCorner);
            _picture.OpacityMask = null;
            _sweepHost.Clip = HudPlate.Outline(size);
            _sweep.Width = _iconSize;
            _sweep.Height = _iconSize * 3;
        }
        else if (dot)
        {
            _picture.Margin = new Thickness(0);
            _picture.Clip = new EllipseGeometry(new Rect(size));
            _picture.OpacityMask = _dotMask;
        }
        else
        {
            _picture.Margin = new Thickness(0);
            _picture.Clip = null;
            _picture.OpacityMask = null;
        }

        // Folder sheet per style.
        var corner = _iconSize * IconDesign.CornerRatio;
        _folderSheet.Background = null;
        _folderSheet.BorderBrush = null;
        if (hud)
        {
            _folderSheet.CornerRadius = new CornerRadius(0);
            _folderSheet.Padding = new Thickness(_iconSize * 0.2);
        }
        else if (dot)
        {
            _folderSheet.SetResourceReference(Border.BackgroundProperty, "Skin.Fill.Folder");
            _folderSheet.SetResourceReference(Border.BorderBrushProperty, "Skin.Edge");
            _folderSheet.CornerRadius = new CornerRadius(_iconSize / 2);
            _folderSheet.Padding = new Thickness(_iconSize * 0.2);
        }
        else
        {
            _folderSheet.SetResourceReference(Border.BackgroundProperty, "Skin.Fill.Folder");
            _folderSheet.SetResourceReference(Border.BorderBrushProperty, "Skin.Edge");
            _folderSheet.CornerRadius = new CornerRadius(corner);
            _folderSheet.Padding = new Thickness(_iconSize * IconDesign.FolderPaddingRatio);
        }
        // SPEC 5.9: the previews inside a Dot Matrix folder are dotted too.
        _folderGrid.OpacityMask = dot ? _dotMask : null;
        foreach (var cell in _folderGrid.Children.OfType<FrameworkElement>())
        {
            cell.Margin = new Thickness(_iconSize * IconDesign.FolderGapRatio / 2);
        }

        // Outline shapes for the launch ripple and the selection ring.
        Geometry outline = hud ? HudPlate.Outline(size) : dot ? new EllipseGeometry(new Rect(size)) : new RectangleGeometry(new Rect(size), corner, corner);
        _ripple.Data = outline;
        _selection.Data = outline;
        _ripple.StrokeThickness = hud ? 1.5 : 2;
        _ripple.Stroke = hud ? new SolidColorBrush(HudDesign.Cyan) : Brushes.White;
        _ripple.StrokeDashArray = dot ? [0.01, 2.2] : null;
        _ripple.StrokeDashCap = dot ? PenLineCap.Round : PenLineCap.Flat;

        // Hover decorations for the new style: an icon under the mouse keeps its brackets or ring. Edit mode sets its own.
        if (!_arranging || !hud)
        {
            _brackets.BeginAnimation(OpacityProperty, null);
            _brackets.BeginAnimation(HudBrackets.InsetProperty, null);
            _brackets.Stroke = new SolidColorBrush(HudDesign.Cyan);
            _brackets.Inset = _iconHost.IsMouseOver ? IconStyleDesign.HudBracketsLocked : IconStyleDesign.HudBracketsRest;
            _brackets.Opacity = hud && _iconHost.IsMouseOver ? 1 : 0;
        }
        ShowRing(dot && (_arranging || _iconHost.IsMouseOver));
        ApplyNearness();
    }

    /// <summary>The name under the icon (SPEC 5.3, 5.8, 5.9).</summary>
    private void ApplyLabel(Look look)
    {
        _labelFrame.Visibility = _showLabel && !_arranging ? Visibility.Visible : Visibility.Collapsed;
        _labelFrame.Margin = new Thickness(0, IconDesign.WindowMargin + _iconSize + IconDesign.LabelGap - 1, 0, 0);
        switch (_style)
        {
            case StyleSetting.Hud:
                _label.Text = _labelText.ToUpper(Localizer.Instance.Culture);
                _label.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
                _label.FontSize = 9.5;
                _label.Foreground = new SolidColorBrush(HudDesign.LabelInk);
                _label.Effect = null;
                _label.Width = double.NaN;
                _labelFrame.MaxWidth = _iconSize + 30;
                _labelFrame.Background = new SolidColorBrush(ThemeService.WithAlpha(HudDesign.Plate, HudDesign.LabelAlpha));
                _labelFrame.CornerRadius = new CornerRadius(0);
                _labelFrame.Padding = new Thickness(5, 1, 5, 1);
                break;
            case StyleSetting.Dot:
                _label.Text = _labelText;
                _label.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Ui");
                _label.FontSize = 10.5;
                _label.SetResourceReference(TextBlock.ForegroundProperty, "Skin.Ink");
                _label.Effect = null;
                _label.Width = double.NaN;
                _labelFrame.MaxWidth = _iconSize + 30;
                _labelFrame.SetResourceReference(Border.BackgroundProperty, "Skin.Fill.Tooltip");
                _labelFrame.CornerRadius = new CornerRadius(8);
                _labelFrame.Padding = new Thickness(7, 0, 7, 1);
                break;
            default:
                _label.Text = _labelText;
                _label.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Ui");
                _label.FontSize = IconDesign.LabelFontSize;
                _label.Foreground = Brushes.White;
                _label.Effect = _labelShadow;
                _label.Width = _iconSize + IconDesign.LabelExtraWidth;
                _labelFrame.MaxWidth = double.PositiveInfinity;
                _labelFrame.Background = null;
                _labelFrame.Padding = new Thickness(0);
                break;
        }
        _ = look;
    }

    private void RefreshStyledImages()
    {
        Func<ImageSource, ImageSource>? filter = _style switch
        {
            StyleSetting.Hud => IconStyleImages.Hologram,
            StyleSetting.Dot => IconStyleImages.Monochrome,
            _ => null,
        };
        _styledImage.Source = filter is not null && _source is not null ? filter(_source) : null;
        for (var i = 0; i < _folderCells.Count; i++)
        {
            var source = i < _previews.Count ? _previews[i] : null;
            _folderCells[i].Styled.Source = filter is not null && source is not null ? filter(source) : null;
        }
        ApplyNearness();
    }

    /// <summary>Hologram fades, glow and edge brighten (HUD) or dots swell (Dot Matrix) as the mouse comes near.</summary>
    private void ApplyNearness()
    {
        var t = Math.Clamp(Math.Max(_arranging ? 1 : _nearness, (double)GetValue(BootNearnessProperty)), 0, 1);
        switch (_style)
        {
            case StyleSetting.Hud:
                _hudPlate.Nearness = t;
                _hudGlow.BlurRadius = IconStyleDesign.HudGlowBase + IconStyleDesign.HudGlowGrow * t;
                _hudGlow.Opacity = 0.12 + 0.4 * t;
                _styledImage.Opacity = 1 - t;
                _scanLines.Opacity = 1 - t;
                break;
            case StyleSetting.Dot:
                var radius = IconStyleDesign.DotRadiusRest + IconStyleDesign.DotRadiusGrow * t;
                _dotShape.RadiusX = radius;
                _dotShape.RadiusY = radius;
                _styledImage.Opacity = 1 - t;
                break;
        }
        foreach (var (_, styled) in _folderCells)
        {
            styled.Opacity = 1 - t;
        }
    }

    private void ShowHover(bool hovering)
    {
        switch (_style)
        {
            case StyleSetting.Hud when !_arranging:
                _brackets.BeginAnimation(OpacityProperty, null);
                _brackets.Stroke = new SolidColorBrush(HudDesign.Cyan);
                if (_reduceMotion)
                {
                    _brackets.Inset = IconStyleDesign.HudBracketsLocked;
                    _brackets.Opacity = hovering ? 1 : 0;
                    return;
                }
                _brackets.BeginAnimation(HudBrackets.InsetProperty, new DoubleAnimation(
                    hovering ? IconStyleDesign.HudBracketsLocked : IconStyleDesign.HudBracketsRest, IconStyleDesign.HudLock) { EasingFunction = Motion.Spring });
                _brackets.BeginAnimation(OpacityProperty, new DoubleAnimation(hovering ? 1 : 0, TimeSpan.FromMilliseconds(180)));
                if (hovering)
                {
                    PlaySweep(TimeSpan.Zero);
                }
                break;
            case StyleSetting.Dot when !_arranging:
                ShowRing(hovering);
                break;
        }
    }

    private void ShowRing(bool shown)
    {
        _ringSpin.BeginAnimation(RotateTransform.AngleProperty, null);
        if (!shown)
        {
            _ring.BeginAnimation(OpacityProperty, _reduceMotion ? null : new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
            _ring.Opacity = 0;
            return;
        }
        _ring.BeginAnimation(OpacityProperty, null);
        _ring.Opacity = 0.9;
        if (_reduceMotion)
        {
            return;
        }
        var grow = new DoubleAnimation(0.85, 1, IconStyleDesign.DotRingGrow) { EasingFunction = Motion.Spring };
        _ringGrow.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _ringGrow.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        var spin = new DoubleAnimation(0, 360, IconStyleDesign.DotRingSpin) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(spin, IconStyleDesign.DotRingFrameRate);
        _ringSpin.BeginAnimation(RotateTransform.AngleProperty, spin);
    }

    /// <summary>A bright line sweeps up across the HUD plate (SPEC 5.8).</summary>
    private void PlaySweep(TimeSpan delay)
    {
        if (_reduceMotion)
        {
            return;
        }
        var move = new DoubleAnimation(0, -2 * _iconSize, IconStyleDesign.HudSweep) { BeginTime = delay, EasingFunction = Motion.Out, FillBehavior = FillBehavior.Stop };
        var show = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = IconStyleDesign.HudSweep, FillBehavior = FillBehavior.Stop };
        show.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        show.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(1)));
        _sweepMove.BeginAnimation(TranslateTransform.YProperty, move);
        _sweep.BeginAnimation(OpacityProperty, show);
    }

    private static SolidColorBrush CreateHitFill()
    {
        var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        brush.Freeze();
        return brush;
    }

    private void ApplyOpacity() =>
        _visuals.Opacity = (_arranging ? 1 : _proximityOpacity) * (IsTargetMissing ? IconDesign.MissingOpacity : 1);
}
