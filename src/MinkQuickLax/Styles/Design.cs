using System.Windows.Media;

namespace MinkQuickLax.Styles;

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

    /// <summary>Hovered row: white from this at the top to this at the bottom.</summary>
    public const double HoverTop = 0.32;
    public const double HoverBottom = 0.18;

    /// <summary>Accent button: white gloss fading down from the top, and a rim lit from above.</summary>
    public const double AccentGloss = 0.30;
    public const double AccentGlossEnd = 0.6;
    public const double AccentRim = 0.55;
}

/// <summary>Corner radii of each style (SPEC 5.2, 5.8, 5.9, 5.10).</summary>
public sealed record StyleRadii(double Surface, double Row, double Field, double Button, double Segment, double Tooltip)
{
    public static readonly StyleRadii Glass = new(Surface: 8, Row: 8, Field: 8, Button: 8, Segment: 7, Tooltip: 8);
    public static readonly StyleRadii Hud = new(Surface: 0, Row: 0, Field: 0, Button: 0, Segment: 0, Tooltip: 0);
    public static readonly StyleRadii Dot = new(Surface: 18, Row: 9, Field: 11, Button: 99, Segment: 99, Tooltip: 14);
}

/// <summary>HUD style (SPEC 5.8).</summary>
public static class HudDesign
{
    public static readonly Color Cyan = Color.FromRgb(0x6F, 0xF3, 0xFF);
    public static readonly Color Amber = Color.FromRgb(0xFF, 0xB2, 0x3E);
    public static readonly Color Red = Color.FromRgb(0xFF, 0x4D, 0x6D);
    public static readonly Color Plate = Color.FromRgb(0x03, 0x0A, 0x10);
    public static readonly Color Ink = Color.FromRgb(0xDD, 0xF7, 0xFF);
    public static readonly Color Ink2 = Color.FromArgb(0xA3, 0xB4, 0xDE, 0xEB);
    public static readonly Color AccentInk = Color.FromRgb(0x02, 0x10, 0x16);
    public static readonly Color LabelInk = Color.FromRgb(0xCF, 0xF6, 0xFF);

    public const double PlateAlpha = 0.86;
    public const double TooltipAlpha = 0.92;
    public const double ReadoutAlpha = 0.88;
    public const double IconPlateAlpha = 0.80;
    public const double LabelAlpha = 0.76;
    public const double EdgeAlpha = 0.26;
    public const double ScanLineAlpha = 0.03;
    public const double HoverAlpha = 0.10;
    public const double SelectedAlpha = 0.16;
    public const double SelectedEdgeAlpha = 0.50;

    /// <summary>Corner cut of surfaces, and the cyan accents drawn at two corners.</summary>
    public const double SurfaceCut = 10;
    public const double CornerAccentLength = 12;
    public const double CornerAccentThickness = 2;
    public const double ScanLinePeriod = 3;
}

/// <summary>Dot Matrix style (SPEC 5.9).</summary>
public static class DotDesign
{
    public static readonly Color LightPlate = Color.FromRgb(0xF4, 0xF4, 0xF1);
    public static readonly Color LightInk = Color.FromRgb(0x11, 0x11, 0x11);
    public static readonly Color LightInk2 = Color.FromRgb(0x67, 0x67, 0x63);
    public static readonly Color LightLine = Color.FromArgb(0x21, 0, 0, 0);
    public static readonly Color LightRed = Color.FromRgb(0xE0, 0x26, 0x1C);
    public static readonly Color DarkPlate = Color.FromRgb(0x14, 0x14, 0x14);
    public static readonly Color DarkInk = Color.FromRgb(0xF1, 0xF1, 0xED);
    public static readonly Color DarkInk2 = Color.FromRgb(0x9B, 0x9B, 0x96);
    public static readonly Color DarkLine = Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF);
    public static readonly Color DarkRed = Color.FromRgb(0xFF, 0x3B, 0x30);

    public const double HoverAlpha = 0.07;
    public const double FieldAlpha = 0.06;
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

    // Folder previews (Glass): each 36% wide, 8% apart, so 10% from each side.
    public const double FolderPreviewRatio = 0.36;
    public const double FolderGapRatio = 0.08;

    /// <summary>How long a dragged icon rests on another before they would become a group (SPEC 4.4).</summary>
    public static readonly TimeSpan GroupHold = TimeSpan.FromMilliseconds(600);

    /// <summary>Group panel grid cell: icon size plus this much (SPEC 5.2).</summary>
    public const double PanelCellExtra = 24;
    public const double PanelLabelHeight = 18;

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
    /// <summary>Space between a dragged icon and its coordinate label.</summary>
    public const double ReadoutGap = 12;
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
