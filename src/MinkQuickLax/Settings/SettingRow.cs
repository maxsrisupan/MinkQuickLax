using System.Windows;
using System.Windows.Controls;

namespace MinkQuickLax.Settings;

public enum SettingRowLayout
{
    /// <summary>Name and hint on the left, the control on the right (switches, buttons, choices).</summary>
    Inline,

    /// <summary>Name on the left and the value on the right, the control full width underneath (sliders, mockup .range).</summary>
    Stacked,
}

/// <summary>One setting in the settings window: its name, an optional hint and value, and the control (SPEC 5.10).</summary>
public sealed class SettingRow : ContentControl
{
    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(SettingRow), new PropertyMetadata(""));
    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(nameof(Hint), typeof(string), typeof(SettingRow), new PropertyMetadata(""));
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(SettingRow), new PropertyMetadata(""));
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(nameof(Layout), typeof(SettingRowLayout), typeof(SettingRow), new PropertyMetadata(SettingRowLayout.Inline));
    public static readonly DependencyProperty IsHighlightedProperty = DependencyProperty.Register(nameof(IsHighlighted), typeof(bool), typeof(SettingRow), new PropertyMetadata(false));

    /// <summary>Briefly outlined after the search box jumped here.</summary>
    public bool IsHighlighted
    {
        get => (bool)GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    static SettingRow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingRow), new FrameworkPropertyMetadata(typeof(SettingRow)));
        FocusableProperty.OverrideMetadata(typeof(SettingRow), new FrameworkPropertyMetadata(false));
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public SettingRowLayout Layout
    {
        get => (SettingRowLayout)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }
}

/// <summary>Title of a group of settings (mockup .ins-group h3); capitals with a red dot in Dot Matrix, cyan capitals in HUD.</summary>
public sealed class SettingsGroupHeader : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(SettingsGroupHeader), new PropertyMetadata(""));

    static SettingsGroupHeader()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingsGroupHeader), new FrameworkPropertyMetadata(typeof(SettingsGroupHeader)));
        FocusableProperty.OverrideMetadata(typeof(SettingsGroupHeader), new FrameworkPropertyMetadata(false));
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

/// <summary>The small picture on each style choice (mockup .pv-hud, .pv-dot, .pv-glass), 20 × 20.</summary>
public sealed class StylePreview : FrameworkElement
{
    public static readonly DependencyProperty StyleKindProperty = DependencyProperty.Register(
        nameof(StyleKind), typeof(Core.Model.StyleSetting), typeof(StylePreview), new FrameworkPropertyMetadata(Core.Model.StyleSetting.Glass, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The dots' color: the choice's text color, so they stay visible when Dot Matrix inverts the selected choice.</summary>
    public static readonly DependencyProperty InkProperty = System.Windows.Documents.TextElement.ForegroundProperty.AddOwner(
        typeof(StylePreview), new FrameworkPropertyMetadata(System.Windows.Media.Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public StylePreview()
    {
        Width = 20;
        Height = 20;
    }

    public Core.Model.StyleSetting StyleKind
    {
        get => (Core.Model.StyleSetting)GetValue(StyleKindProperty);
        set => SetValue(StyleKindProperty, value);
    }

    public System.Windows.Media.Brush Ink
    {
        get => (System.Windows.Media.Brush)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        const double Size = 20;
        switch (StyleKind)
        {
            case Core.Model.StyleSetting.Hud:
            {
                var cyan = new System.Windows.Media.SolidColorBrush(Styles.HudDesign.Cyan);
                var plate = System.Windows.Media.Geometry.Parse("M6,0 L20,0 L20,14 L14,20 L0,20 L0,6 Z");
                drawingContext.DrawGeometry(new System.Windows.Media.SolidColorBrush(Styles.HudDesign.Plate), new System.Windows.Media.Pen(cyan, 1.5), plate);
                drawingContext.DrawEllipse(cyan, null, new Point(Size / 2, Size / 2), 2.2, 2.2);
                break;
            }
            case Core.Model.StyleSetting.Dot:
            {
                // A disc of 4 px dots with a red status dot, like the mockup.
                for (var y = 2.0; y < Size; y += 4)
                {
                    for (var x = 2.0; x < Size; x += 4)
                    {
                        if ((x - Size / 2) * (x - Size / 2) + (y - Size / 2) * (y - Size / 2) <= Size * Size / 4)
                        {
                            drawingContext.DrawEllipse(Ink, null, new Point(x, y), 1.1, 1.1);
                        }
                    }
                }
                drawingContext.DrawEllipse(new System.Windows.Media.SolidColorBrush(Styles.DotDesign.LightRed), null, new Point(Size - 1, 1), 3, 3);
                break;
            }
            default:
            {
                var orb = new System.Windows.Media.LinearGradientBrush(
                    [
                        new System.Windows.Media.GradientStop(Styles.IconDesign.LetterPalette[0].From, 0),
                        new System.Windows.Media.GradientStop(Styles.IconDesign.LetterPalette[2].From, 0.4),
                        new System.Windows.Media.GradientStop(Styles.IconDesign.LetterPalette[3].From, 0.7),
                        new System.Windows.Media.GradientStop(Styles.IconDesign.LetterPalette[1].From, 1),
                    ],
                    new Point(0, 0), new Point(1, 1));
                drawingContext.DrawEllipse(orb, null, new Point(Size / 2, Size / 2), Size / 2, Size / 2);
                var shine = new System.Windows.Media.RadialGradientBrush(System.Windows.Media.Color.FromArgb(0xE6, 255, 255, 255), System.Windows.Media.Color.FromArgb(0, 255, 255, 255));
                drawingContext.DrawEllipse(shine, null, new Point(6.4, 5.6), 4, 3.5);
                break;
            }
        }
    }
}
