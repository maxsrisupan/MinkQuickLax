using System.Windows;
using System.Windows.Media;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Styles.Glass;

namespace MinkQuickLax.Services;

/// <summary>The resolved look after combining app settings with Windows settings.</summary>
/// <param name="Blur">Blurred surfaces may use acrylic; otherwise they use the solid fill.</param>
public sealed record Look(bool Dark, bool HighContrast, bool Blur, bool ReduceMotion);

/// <summary>Keeps the glass resources in sync with the theme, tint, transparency and motion settings (SPEC 5.1, 5.5).</summary>
public sealed class ThemeService
{
    private static readonly Uri LightTheme = new("pack://application:,,,/Styles/Glass/Theme.Light.xaml");
    private static readonly Uri DarkTheme = new("pack://application:,,,/Styles/Glass/Theme.Dark.xaml");

    private readonly ResourceDictionary _resources;
    private ResourceDictionary? _themeDictionary;

    public ThemeService(Application application)
    {
        _resources = application.Resources;
        Current = new Look(Dark: true, HighContrast: false, Blur: false, ReduceMotion: false);
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
        var blur = DwmBackdrop.IsBlurSupported && settings.Glass.Blur && system.TransparencyEnabled && !system.BatterySaver && !system.HighContrast;
        var look = new Look(dark, system.HighContrast, blur, reduceMotion);

        SwapTheme(dark);
        if (system.HighContrast)
        {
            ApplyHighContrast();
        }
        else
        {
            ApplyGlass(settings.Glass, blur);
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

    private void ApplyGlass(GlassSettings glass, bool blur)
    {
        var rgb = glass.Tint switch
        {
            GlassTint.Lagoon => IconDesign.Lagoon,
            GlassTint.Ember => IconDesign.Ember,
            GlassTint.Ink => IconDesign.Ink,
            _ => Resolve("Glass.Rgb"),
        };
        SetBrush("Glass.Ink", Resolve("Glass.InkColor"));
        SetBrush("Glass.Ink2", Resolve("Glass.Ink2Color"));
        SetBrush("Glass.Edge", Resolve("Glass.EdgeColor"));
        SetBrush("Glass.Highlight", Resolve("Glass.HighlightColor"));
        SetBrush("Glass.HoverFill", Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF));
        SetBrush("Accent", Resolve("AccentColor"));
        SetBrush("AccentInk", Resolve("AccentInkColor"));
        SetBrush("Danger", Resolve("DangerColor"));
        SetBrush("Guide", Resolve("GuideColor"));

        _resources["Glass.Fill.Tooltip"] = GlassFill(rgb, blur ? GlassAlpha.Tooltip : GlassAlpha.Solid);
        _resources["Glass.Fill.Menu"] = GlassFill(rgb, blur ? GlassAlpha.Menu : GlassAlpha.Solid);
        _resources["Glass.Fill.Notice"] = GlassFill(rgb, blur ? GlassAlpha.Menu : GlassAlpha.Solid);
        _resources["Glass.Fill.Panel"] = GlassFill(rgb, blur ? glass.TintStrength : GlassAlpha.Solid);
        _resources["Glass.Fill.Folder"] = GlassFill(rgb, GlassAlpha.Folder);
        _resources["Glass.Fill.Readout"] = GlassFill(rgb, GlassAlpha.DragReadout);
    }

    private void ApplyHighContrast()
    {
        _resources["Glass.Ink"] = SystemColors.WindowTextBrush;
        _resources["Glass.Ink2"] = SystemColors.GrayTextBrush;
        _resources["Glass.Edge"] = SystemColors.WindowFrameBrush;
        _resources["Glass.Highlight"] = Brushes.Transparent;
        _resources["Glass.HoverFill"] = SystemColors.HighlightBrush;
        _resources["Accent"] = SystemColors.HighlightBrush;
        _resources["AccentInk"] = SystemColors.HighlightTextBrush;
        _resources["Danger"] = SystemColors.WindowTextBrush;
        _resources["Guide"] = SystemColors.HighlightBrush;
        foreach (var key in new[] { "Glass.Fill.Tooltip", "Glass.Fill.Menu", "Glass.Fill.Notice", "Glass.Fill.Panel", "Glass.Fill.Folder", "Glass.Fill.Readout" })
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

    private static Color WithAlpha(Color rgb, double alpha) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), rgb.R, rgb.G, rgb.B);
}
