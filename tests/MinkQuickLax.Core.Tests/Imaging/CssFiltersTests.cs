using MinkQuickLax.Core.Imaging;

namespace MinkQuickLax.Core.Tests.Imaging;

/// <summary>
/// Expected bytes were read from Microsoft Edge rendering swatches with the mockup's CSS filters, so the icons match the
/// mockup rather than an approximation of it.
/// </summary>
public sealed class CssFiltersTests
{
    [Theory]
    [InlineData(255, 0, 0, 37, 83, 100)]
    [InlineData(0, 255, 0, 124, 255, 255)]
    [InlineData(0, 0, 255, 13, 28, 34)]
    [InlineData(255, 255, 255, 255, 255, 255)]
    [InlineData(128, 128, 128, 87, 196, 235)]
    [InlineData(30, 30, 30, 20, 46, 55)]
    [InlineData(40, 120, 220, 75, 169, 203)]
    [InlineData(220, 60, 120, 67, 151, 181)]
    [InlineData(0, 0, 0, 0, 0, 0)]
    public void Hologram_MatchesTheBrowser(int r, int g, int b, int expectedR, int expectedG, int expectedB)
    {
        var (fr, fg, fb) = CssFilters.Hologram(r / 255.0, g / 255.0, b / 255.0);

        Assert.Equal((expectedR, expectedG, expectedB), (Byte(fr), Byte(fg), Byte(fb)));
    }

    [Theory]
    [InlineData(255, 0, 0, 22)]
    [InlineData(0, 255, 0, 255)]
    [InlineData(128, 128, 128, 160)]
    [InlineData(30, 30, 30, 0)]
    [InlineData(40, 120, 220, 127)]
    [InlineData(220, 60, 120, 105)]
    [InlineData(90, 200, 150, 245)]
    public void Monochrome_MatchesTheBrowser(int r, int g, int b, int expected)
    {
        Assert.Equal(expected, Byte(CssFilters.Monochrome(r / 255.0, g / 255.0, b / 255.0)));
    }

    private static int Byte(double value) => (int)Math.Round(value * 255);
}
