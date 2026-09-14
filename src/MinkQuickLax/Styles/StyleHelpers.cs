using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;

namespace MinkQuickLax.Styles;

/// <summary>A border whose background can have HUD cut corners (top-left and bottom-right) instead of rounded ones.</summary>
public sealed class CutBorder : Border
{
    public static readonly DependencyProperty CutProperty = DependencyProperty.Register(
        nameof(Cut), typeof(double), typeof(CutBorder), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Cut
    {
        get => (double)GetValue(CutProperty);
        set => SetValue(CutProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (Cut <= 0 || size.Width <= 0 || size.Height <= 0)
        {
            base.OnRender(dc);
            return;
        }
        var cut = Math.Min(Cut, Math.Min(size.Width, size.Height) / 2);
        var figure = new PathFigure { StartPoint = new Point(cut, 0), IsClosed = true };
        figure.Segments.Add(new PolyLineSegment(
            [new Point(size.Width, 0), new Point(size.Width, size.Height - cut), new Point(size.Width - cut, size.Height), new Point(0, size.Height), new Point(0, cut)],
            isStroked: true));
        var pen = BorderBrush is { } edge && BorderThickness.Left > 0 ? new Pen(edge, BorderThickness.Left) : null;
        dc.DrawGeometry(Background, pen, new PathGeometry([figure]));
    }
}

/// <summary>
/// Text that is written in capitals in HUD and Dot Matrix (menu headers, group titles, toolbar status) and as-is in
/// Glass (SPEC 5.8, 5.9). WPF has no text-transform, so the text is transformed here.
/// </summary>
public sealed class LabelText : TextBlock
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(string), typeof(LabelText), new PropertyMetadata("", (d, _) => ((LabelText)d).Refresh()));

    public static readonly DependencyProperty KindProperty = Skin.KindProperty.AddOwner(
        typeof(LabelText), new FrameworkPropertyMetadata(StyleSetting.Glass, FrameworkPropertyMetadataOptions.Inherits, (d, _) => ((LabelText)d).Refresh()));

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private void Refresh()
    {
        var kind = (StyleSetting)GetValue(KindProperty);
        Text = kind == StyleSetting.Glass ? Source : Source.ToUpper(Localizer.Instance.Culture);
    }
}

/// <summary>
/// A normal window (taskbar, Alt+Tab, can take focus) drawn as a plate in the current style: the scanner and the
/// settings window (SPEC 4.8, 5.5). Glass gets the system acrylic and rounded corners; HUD and Dot Matrix draw their own
/// shape on a transparent window.
/// </summary>
public class StyledWindow : Window
{
    private ThemeService? _theme;

    public StyledWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
    }

    /// <summary>Height of the draggable caption area at the top.</summary>
    protected double CaptionHeight { get; init; } = 56;

    /// <summary>Call from the constructor before the window is shown.</summary>
    protected void UseTheme(ThemeService theme)
    {
        _theme = theme;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            GlassFrameThickness = new Thickness(-1),
            CaptionHeight = CaptionHeight,
            ResizeBorderThickness = new Thickness(6),
            UseAeroCaptionButtons = false,
        });
        Skin.SetKind(this, theme.Current.Style);
        theme.Changed += OnLookChanged;
        Closed += (_, _) => theme.Changed -= OnLookChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        if (HwndSource.FromHwnd(handle) is { } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
        if (_theme is not null)
        {
            ApplyLook(_theme.Current);
        }
    }

    private void OnLookChanged(Look look) => ApplyLook(look);

    private void ApplyLook(Look look)
    {
        Skin.SetKind(this, look.Style);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }
        DwmBackdrop.SetDarkFrame(handle, look.SurfacesDark);
        DwmBackdrop.SetRoundedCorners(handle, rounded: look.Style == StyleSetting.Glass);
        DwmBackdrop.SetSystemAcrylic(handle, look.Blur);
    }
}
