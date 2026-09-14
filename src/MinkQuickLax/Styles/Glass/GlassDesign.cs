using System.Windows.Media;

namespace MinkQuickLax.Styles.Glass;

/// <summary>Tint opacity ("a") of each glass surface (SPEC 5.2).</summary>
public static class GlassAlpha
{
    public const double Tooltip = 0.82;
    public const double Menu = 0.58;
    public const double Scanner = 0.42;
    public const double Folder = 0.55;
    public const double DragReadout = 0.78;

    /// <summary>Used instead of the surface's own value when blur is unavailable or turned off (SPEC 5.5).</summary>
    public const double Solid = 0.92;

    // Gradient recipe: a+0.12 at the start, a at 55%, a+0.05 at the end, 135°.
    public const double GradientStartBoost = 0.12;
    public const double GradientMiddleOffset = 0.55;
    public const double GradientEndBoost = 0.05;
}

/// <summary>Icon geometry (SPEC 5.3).</summary>
public static class IconDesign
{
    public const double WindowMargin = 12;
    public const double CornerRatio = 0.29;
    public const double LetterSizeRatio = 0.40;
    public const double LabelGap = 9;
    public const double LabelFontSize = 11;
    public const double LabelExtraWidth = 20;
    public const double LabelHeight = 16;
    public const double TooltipGap = 10;
    public const double TooltipFontSize = 12.5;
    public const double MissingOpacity = 0.4;
    public const double MaxMagnify = 1.16;

    /// <summary>The eight letter-icon gradients, 150° from light to dark.</summary>
    public static readonly (Color From, Color To)[] LetterPalette =
    [
        (Rgb(0x2FD9BE), Rgb(0x1E8F9E)),
        (Rgb(0x2F6FD9), Rgb(0x1B3F8F)),
        (Rgb(0xFFC15A), Rgb(0xE08A1E)),
        (Rgb(0xFF7250), Rgb(0xC73E2A)),
        (Rgb(0xFF5C8A), Rgb(0xB8325F)),
        (Rgb(0x8E6CF0), Rgb(0x5A3FB8)),
        (Rgb(0x7ED67A), Rgb(0x3E9A4E)),
        (Rgb(0x7F95A3), Rgb(0x4B5F69)),
    ];

    /// <summary>Glass tint presets besides "follow the theme" (SPEC 5.1).</summary>
    public static readonly Color Lagoon = Color.FromRgb(47, 217, 190);
    public static readonly Color Ember = Color.FromRgb(255, 114, 80);
    public static readonly Color Ink = Color.FromRgb(10, 22, 28);

    private static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}

/// <summary>Motion timings and curves (SPEC 5.4).</summary>
public static class Motion
{
    public static readonly TimeSpan Proximity = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan TooltipFade = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan TooltipRise = TimeSpan.FromMilliseconds(250);
    public const double TooltipRiseDistance = 6;
    public static readonly TimeSpan LaunchHop = TimeSpan.FromMilliseconds(750);
    public static readonly TimeSpan LaunchRipple = TimeSpan.FromMilliseconds(700);
    public static readonly TimeSpan MenuOpen = TimeSpan.FromMilliseconds(160);
    public const double MenuStartScale = 0.95;
    public static readonly TimeSpan HideShow = TimeSpan.FromMilliseconds(280);
    public static readonly TimeSpan HideShowStagger = TimeSpan.FromMilliseconds(14);
    public const double HiddenScale = 0.8;
    public static readonly TimeSpan Jiggle = TimeSpan.FromMilliseconds(260);
    public const double JiggleAngle = 1.6;
    public const double DragScale = 1.12;
    public static readonly TimeSpan ToolbarIn = TimeSpan.FromMilliseconds(450);
    public const double ToolbarTopGap = 14;
    public const double AlignThreshold = 8;
    public const double ReadoutOffset = 18;
    public const double GuideDash = 6;
    public const double GuideGap = 5;
    public static readonly TimeSpan Tidy = TimeSpan.FromMilliseconds(550);
    public static readonly TimeSpan AddPop = TimeSpan.FromMilliseconds(600);
    public static readonly TimeSpan AddPopStagger = TimeSpan.FromMilliseconds(90);

    /// <summary>cubic-bezier(.34,1.56,.64,1)</summary>
    public static readonly CubicBezierEase Spring = CubicBezierEase.Frozen(0.34, 1.56, 0.64, 1);

    /// <summary>cubic-bezier(.2,.7,.3,1)</summary>
    public static readonly CubicBezierEase Out = CubicBezierEase.Frozen(0.2, 0.7, 0.3, 1);
}
