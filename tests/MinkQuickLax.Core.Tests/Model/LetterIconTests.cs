using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Model;

public sealed class LetterIconTests
{
    [Theory]
    [InlineData("Visual Studio Code", "V")]
    [InlineData("  github", "G")]
    [InlineData("ที่ทำงาน", "ที่")]
    [InlineData("น้ำ", "น้ำ")]
    [InlineData("👩‍💻 Dev", "👩‍💻")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    public void Text_IsTheFirstGrapheme(string? name, string expected)
    {
        Assert.Equal(expected, LetterIcon.For(name).Text);
    }

    [Fact]
    public void Palette_IsStableAndInRange()
    {
        var a = LetterIcon.For("Figma");
        var b = LetterIcon.For("Figma");

        Assert.Equal(a.PaletteIndex, b.PaletteIndex);
        Assert.InRange(a.PaletteIndex, 0, LetterIcon.PaletteSize - 1);
    }

    [Fact]
    public void Palette_SpreadsAcrossNames()
    {
        var used = Enumerable.Range(0, 200).Select(i => LetterIcon.For($"app {i}").PaletteIndex).Distinct().Count();

        Assert.Equal(LetterIcon.PaletteSize, used);
    }
}
