namespace MinkQuickLax.Core.Imaging;

/// <summary>
/// CSS filter functions as the color matrices of the Filter Effects spec, on straight (not premultiplied) sRGB values
/// in 0..1: the mockup's icon filters for HUD and Dot Matrix. Checked against Edge, which gives the same bytes.
/// </summary>
public static class CssFilters
{
    private static readonly double[] Grayscale = Saturation(0, 0.2126, 0.7152, 0.0722);
    private static readonly double[] Sepia90 = Sepia(0.9);
    private static readonly double[] HueRotate150 = HueRotate(150);
    private static readonly double[] Saturate260 = Saturation(2.6, 0.213, 0.715, 0.072);
    private const double Brightness115 = 1.15;
    private const double Contrast150 = 1.5;
    private const double Brightness125 = 1.25;

    public static (double R, double G, double B) Hologram(double r, double g, double b)
    {
        var c = Apply(Grayscale, (r, g, b));
        c = Apply(Sepia90, c);
        c = Apply(HueRotate150, c);
        c = Apply(Saturate260, c);
        return (Math.Clamp(c.R * Brightness115, 0, 1), Math.Clamp(c.G * Brightness115, 0, 1), Math.Clamp(c.B * Brightness115, 0, 1));
    }

    /// <summary>The Dot Matrix icon at rest (SPEC 5.9): <c>grayscale(1) contrast(1.5) brightness(1.25)</c>.</summary>
    public static double Monochrome(double r, double g, double b)
    {
        var (gray, _, _) = Apply(Grayscale, (r, g, b));
        var contrasted = Math.Clamp((gray - 0.5) * Contrast150 + 0.5, 0, 1);
        return Math.Clamp(contrasted * Brightness125, 0, 1);
    }

    /// <summary><c>saturate(s)</c>; <c>grayscale(1)</c> is the same matrix at s = 0 with its own luminance weights.</summary>
    private static double[] Saturation(double s, double wr, double wg, double wb) =>
    [
        wr + (1 - wr) * s, wg - wg * s, wb - wb * s,
        wr - wr * s, wg + (1 - wg) * s, wb - wb * s,
        wr - wr * s, wg - wg * s, wb + (1 - wb) * s,
    ];

    private static double[] Sepia(double amount)
    {
        var s = 1 - amount;
        return
        [
            0.393 + 0.607 * s, 0.769 - 0.769 * s, 0.189 - 0.189 * s,
            0.349 - 0.349 * s, 0.686 + 0.314 * s, 0.168 - 0.168 * s,
            0.272 - 0.272 * s, 0.534 - 0.534 * s, 0.131 + 0.869 * s,
        ];
    }

    private static double[] HueRotate(double degrees)
    {
        var angle = degrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
        return
        [
            0.213 + cos * 0.787 - sin * 0.213, 0.715 - cos * 0.715 - sin * 0.715, 0.072 - cos * 0.072 + sin * 0.928,
            0.213 - cos * 0.213 + sin * 0.143, 0.715 + cos * 0.285 + sin * 0.140, 0.072 - cos * 0.072 - sin * 0.283,
            0.213 - cos * 0.213 - sin * 0.787, 0.715 - cos * 0.715 + sin * 0.715, 0.072 + cos * 0.928 + sin * 0.072,
        ];
    }

    private static (double R, double G, double B) Apply(double[] m, (double R, double G, double B) c) =>
    (
        Math.Clamp(m[0] * c.R + m[1] * c.G + m[2] * c.B, 0, 1),
        Math.Clamp(m[3] * c.R + m[4] * c.G + m[5] * c.B, 0, 1),
        Math.Clamp(m[6] * c.R + m[7] * c.G + m[8] * c.B, 0, 1)
    );
}
