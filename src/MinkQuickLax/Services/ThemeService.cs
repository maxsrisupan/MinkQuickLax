using System.Windows;
using System.Windows.Media;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Services;

/// <summary>The resolved look after combining app settings with Windows settings.</summary>
/// <param name="Dark">The chosen or Windows theme. HUD surfaces are dark regardless (see <see cref="SurfacesDark"/>).</param>
/// <param name="Blur">Blurred surfaces may use acrylic; otherwise they use a solid fill. Only Glass blurs.</param>
public sealed record Look(bool Dark, bool HighContrast, bool Blur, bool ReduceMotion, StyleSetting Style)
{
    /// <summary>Whether surfaces are drawn light-on-dark: HUD always is (SPEC 5.1).</summary>
    public bool SurfacesDark => Dark || Style == StyleSetting.Hud;
}

/// <summary>
/// Keeps the shared resources in sync with the style, theme, tint, transparency and motion settings (SPEC 5):
/// brushes, fills, corner radii and fonts that every surface reads through DynamicResource.
/// </summary>
public sealed class ThemeService
{
    private static readonly Uri LightTheme = new("pack://application:,,,/Styles/Theme.Light.xaml");
    private static readonly Uri DarkTheme = new("pack://application:,,,/Styles/Theme.Dark.xaml");

    private static readonly string[] FillKeys =
        ["Skin.Fill.Tooltip", "Skin.Fill.Menu", "Skin.Fill.Notice", "Skin.Fill.Panel", "Skin.Fill.Scanner", "Skin.Fill.Folder", "Skin.Fill.Readout", "Skin.Fill.Popup"];

    private readonly ResourceDictionary _resources;
    private ResourceDictionary? _themeDictionary;

    public ThemeService(Application application)
    {
        _resources = application.Resources;
        _resources["Font.Mono"] = CompositeFonts.Mono;
        Current = new Look(Dark: true, HighContrast: false, Blur: false, ReduceMotion: false, Style: StyleSetting.Glass);
    }

    public event Action<Look>? Changed;

    public Look Current { get; private set; }

    public void Apply(AppSettings settings, SystemLook system)
    {
        var dark = settings.Theme switch
        {
            ThemeSetting.Light => false,
            ThemeSetting.Dark => true,
            _ => !system.AppsUseLightTheme,
        };
        var reduceMotion = settings.ReduceMotion switch
        {
            ReduceMotionSetting.On => true,
            ReduceMotionSetting.Off => false,
            _ => !system.AnimationsEnabled,
        };
        var style = Enum.IsDefined(settings.Style) ? settings.Style : StyleSetting.Glass;
        var blur = style == StyleSetting.Glass && DwmBackdrop.IsBlurSupported && settings.Glass.Blur
            && system.TransparencyEnabled && !system.BatterySaver && !system.HighContrast;
        var look = new Look(dark, system.HighContrast, blur, reduceMotion, style);

        SwapTheme(look.SurfacesDark);
        ApplyShape(style);
        if (system.HighContrast)
        {
            ApplyHighContrast();
        }
        else
        {
            switch (style)
            {
                case StyleSetting.Hud:
                    ApplyHud();
                    break;
                case StyleSetting.Dot:
                    ApplyDot(dark);
                    break;
                default:
                    ApplyGlass(settings.Glass, blur);
                    break;
            }
        }

        var changed = look != Current;
        Current = look;
        if (changed)
        {
            Changed?.Invoke(look);
        }
    }

    /// <summary>135° gradient: a+0.12 → a (55%) → a+0.05 (SPEC 5.2).</summary>
    public static Brush GlassFill(Color rgb, double alpha)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        brush.GradientStops.Add(new GradientStop(WithAlpha(rgb, alpha + GlassAlpha.GradientStartBoost), 0));
        brush.GradientStops.Add(new GradientStop(WithAlpha(rgb, alpha), GlassAlpha.GradientMiddleOffset));
        brush.GradientStops.Add(new GradientStop(WithAlpha(rgb, alpha + GlassAlpha.GradientEndBoost), 1));
        brush.Freeze();
        return brush;
    }

    public Color Resolve(string key) => (Color)_resources[key];

    public static Color WithAlpha(Color rgb, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), rgb.R, rgb.G, rgb.B);

    private void SwapTheme(bool dark)
    {
        var next = new ResourceDictionary { Source = dark ? DarkTheme : LightTheme };
        if (_themeDictionary is not null)
        {
            _resources.MergedDictionaries.Remove(_themeDictionary);
        }
        _resources.MergedDictionaries.Add(next);
        _themeDictionary = next;
    }

    /// <summary>Corner radii and fonts that change with the style (SPEC 5.7, 5.10).</summary>
    private void ApplyShape(StyleSetting style)
    {
        var radii = style switch { StyleSetting.Hud => StyleRadii.Hud, StyleSetting.Dot => StyleRadii.Dot, _ => StyleRadii.Glass };
        _resources["Skin.Radius.Surface"] = new CornerRadius(radii.Surface);
        _resources["Skin.Radius.Row"] = new CornerRadius(radii.Row);
        _resources["Skin.Radius.Field"] = new CornerRadius(radii.Field);
        _resources["Skin.Radius.Button"] = new CornerRadius(radii.Button);
        _resources["Skin.Radius.Segment"] = new CornerRadius(radii.Segment);
        _resources["Skin.Radius.Tooltip"] = new CornerRadius(radii.Tooltip);
        _resources["Skin.Cut.Button"] = style == StyleSetting.Hud ? 7.0 : 0.0;

        _resources["Font.Heading"] = _resources[style == StyleSetting.Dot ? "Font.Ui" : "Font.Display"];
        _resources["Font.Label"] = _resources[style == StyleSetting.Glass ? "Font.Ui" : "Font.Mono"];
        _resources["Font.Number"] = _resources[style switch { StyleSetting.Dot => "Font.Dot", StyleSetting.Hud => "Font.Mono", _ => "Font.Ui" }];
        _resources["Font.Readout"] = _resources[style == StyleSetting.Dot ? "Font.Dot" : "Font.Mono"];
    }

    private void ApplyGlass(GlassSettings glass, bool blur)
    {
        var rgb = glass.Tint switch
        {
            GlassTint.Lagoon => IconDesign.Lagoon,
            GlassTint.Ember => IconDesign.Ember,
            GlassTint.Ink => IconDesign.Ink,
            _ => Resolve("Skin.Rgb"),
        };
        var ink = Resolve("Skin.InkColor");
        SetBrush("Skin.Ink", ink);
        SetBrush("Skin.Ink2", Resolve("Skin.Ink2Color"));
        SetBrush("Skin.Edge", Resolve("Skin.EdgeColor"));
        SetBrush("Skin.Highlight", Resolve("Skin.HighlightColor"));
        SetBrush("Skin.HoverFill", Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF));
        SetBrush("Skin.RowHoverEdge", Resolve("Skin.EdgeColor"));
        SetBrush("Skin.RowSelectedFill", WithAlpha(Resolve("AccentColor"), 0.18));
        SetBrush("Skin.FieldFill", Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
        SetBrush("Skin.SelectedFill", WithAlpha(rgb, 0.75));
        SetBrush("Skin.SelectedEdge", Resolve("Skin.EdgeColor"));
        SetBrush("Skin.SelectedInk", ink);
        SetBrush("Skin.Track", WithAlpha(ink, 0.24));
        SetBrush("Skin.Amber", Resolve("GuideColor"));
        SetBrush("Skin.ScanLines", Colors.Transparent);
        SetBrush("Accent", Resolve("AccentColor"));
        SetBrush("AccentInk", Resolve("AccentInkColor"));
        SetBrush("Danger", Resolve("DangerColor"));
        SetBrush("Guide", Resolve("GuideColor"));

        _resources["Skin.Fill.Tooltip"] = GlassFill(rgb, blur ? GlassAlpha.Tooltip : GlassAlpha.Solid);
        _resources["Skin.Fill.Menu"] = GlassFill(rgb, blur ? GlassAlpha.Menu : GlassAlpha.Solid);
        _resources["Skin.Fill.Notice"] = GlassFill(rgb, blur ? GlassAlpha.Menu : GlassAlpha.Solid);
        _resources["Skin.Fill.Panel"] = GlassFill(rgb, blur ? glass.TintStrength : GlassAlpha.Solid);
        _resources["Skin.Fill.Scanner"] = GlassFill(rgb, blur ? GlassAlpha.Scanner : GlassAlpha.Solid);
        _resources["Skin.Fill.Folder"] = GlassFill(rgb, GlassAlpha.Folder);
        _resources["Skin.Fill.Readout"] = GlassFill(rgb, GlassAlpha.DragReadout);
        _resources["Skin.Fill.Popup"] = GlassFill(rgb, GlassAlpha.Solid);
    }

    /// <summary>SPEC 5.8: dark plates with cyan edges, no blur, whatever the theme.</summary>
    private void ApplyHud()
    {
        SetBrush("Skin.Ink", HudDesign.Ink);
        SetBrush("Skin.Ink2", HudDesign.Ink2);
        SetBrush("Skin.Edge", WithAlpha(HudDesign.Cyan, HudDesign.EdgeAlpha));
        SetBrush("Skin.Highlight", Colors.Transparent);
        SetBrush("Skin.HoverFill", WithAlpha(HudDesign.Cyan, HudDesign.HoverAlpha));
        SetBrush("Skin.RowHoverEdge", WithAlpha(HudDesign.Cyan, 0.30));
        SetBrush("Skin.RowSelectedFill", WithAlpha(HudDesign.Cyan, HudDesign.SelectedAlpha));
        SetBrush("Skin.FieldFill", WithAlpha(HudDesign.Ink, 0.08));
        SetBrush("Skin.SelectedFill", WithAlpha(HudDesign.Cyan, HudDesign.SelectedAlpha));
        SetBrush("Skin.SelectedEdge", WithAlpha(HudDesign.Cyan, HudDesign.SelectedEdgeAlpha));
        SetBrush("Skin.SelectedInk", HudDesign.Ink);
        SetBrush("Skin.Track", WithAlpha(HudDesign.Cyan, 0.24));
        SetBrush("Skin.Amber", HudDesign.Amber);
        SetBrush("Skin.ScanLines", WithAlpha(HudDesign.Cyan, HudDesign.ScanLineAlpha));
        SetBrush("Accent", HudDesign.Cyan);
        SetBrush("AccentInk", HudDesign.AccentInk);
        SetBrush("Danger", HudDesign.Red);
        SetBrush("Guide", HudDesign.Cyan);

        var plate = WithAlpha(HudDesign.Plate, HudDesign.PlateAlpha);
        foreach (var key in FillKeys)
        {
            SetBrush(key, plate);
        }
        SetBrush("Skin.Fill.Tooltip", WithAlpha(HudDesign.Plate, HudDesign.TooltipAlpha));
        SetBrush("Skin.Fill.Readout", WithAlpha(HudDesign.Plate, HudDesign.ReadoutAlpha));
        SetBrush("Skin.Fill.Popup", WithAlpha(HudDesign.Plate, HudDesign.TooltipAlpha));
    }

    /// <summary>SPEC 5.9: solid plates that follow the theme; the accent is the ink itself.</summary>
    private void ApplyDot(bool dark)
    {
        var plate = dark ? DotDesign.DarkPlate : DotDesign.LightPlate;
        var ink = dark ? DotDesign.DarkInk : DotDesign.LightInk;
        var line = dark ? DotDesign.DarkLine : DotDesign.LightLine;
        var red = dark ? DotDesign.DarkRed : DotDesign.LightRed;
        SetBrush("Skin.Ink", ink);
        SetBrush("Skin.Ink2", dark ? DotDesign.DarkInk2 : DotDesign.LightInk2);
        SetBrush("Skin.Edge", line);
        SetBrush("Skin.Highlight", Colors.Transparent);
        SetBrush("Skin.HoverFill", WithAlpha(ink, DotDesign.HoverAlpha));
        SetBrush("Skin.RowHoverEdge", Colors.Transparent);
        SetBrush("Skin.RowSelectedFill", WithAlpha(ink, DotDesign.HoverAlpha));
        SetBrush("Skin.FieldFill", WithAlpha(ink, DotDesign.FieldAlpha));
        SetBrush("Skin.SelectedFill", ink);
        SetBrush("Skin.SelectedEdge", ink);
        SetBrush("Skin.SelectedInk", plate);
        SetBrush("Skin.Track", line);
        SetBrush("Skin.Amber", red);
        SetBrush("Skin.ScanLines", Colors.Transparent);
        SetBrush("Accent", ink);
        SetBrush("AccentInk", plate);
        SetBrush("Danger", red);
        SetBrush("Guide", red);
        foreach (var key in FillKeys)
        {
            SetBrush(key, plate);
        }
    }

    private void ApplyHighContrast()
    {
        _resources["Skin.Ink"] = SystemColors.WindowTextBrush;
        _resources["Skin.Ink2"] = SystemColors.GrayTextBrush;
        _resources["Skin.Edge"] = SystemColors.WindowFrameBrush;
        _resources["Skin.Highlight"] = Brushes.Transparent;
        _resources["Skin.HoverFill"] = SystemColors.HighlightBrush;
        _resources["Skin.RowHoverEdge"] = SystemColors.HighlightBrush;
        _resources["Skin.RowSelectedFill"] = SystemColors.HighlightBrush;
        _resources["Skin.FieldFill"] = SystemColors.ControlBrush;
        _resources["Skin.SelectedFill"] = SystemColors.HighlightBrush;
        _resources["Skin.SelectedEdge"] = SystemColors.HighlightBrush;
        _resources["Skin.SelectedInk"] = SystemColors.HighlightTextBrush;
        _resources["Skin.Track"] = SystemColors.GrayTextBrush;
        _resources["Skin.Amber"] = SystemColors.HighlightBrush;
        _resources["Skin.ScanLines"] = Brushes.Transparent;
        _resources["Accent"] = SystemColors.HighlightBrush;
        _resources["AccentInk"] = SystemColors.HighlightTextBrush;
        _resources["Danger"] = SystemColors.WindowTextBrush;
        _resources["Guide"] = SystemColors.HighlightBrush;
        foreach (var key in FillKeys)
        {
            _resources[key] = SystemColors.WindowBrush;
        }
    }

    private void SetBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _resources[key] = brush;
    }
}
