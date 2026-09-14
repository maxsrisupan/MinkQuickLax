using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Model;

public sealed class TextSearchTests
{
    [Theory]
    [InlineData("ขนาด", 3)]
    [InlineData("ไอคอน", 2)]
    [InlineData("icon", 3)]
    [InlineData("SIZE", 2)]
    [InlineData("ใหญ่", 1)]
    [InlineData("bigger", 1)]
    [InlineData("hotkey", 0)]
    [InlineData("   ", 0)]
    public void MatchesNamesAndKeywordsInBothLanguages(string query, int expected)
    {
        var score = TextSearch.Score(query, ["ขนาดไอคอน", "Icon size"], ["ใหญ่ เล็ก", "bigger smaller"]);

        Assert.Equal(expected, score);
    }

    [Fact]
    public void FullWidthLettersMatch()
    {
        Assert.Equal(3, TextSearch.Score("ｉｃｏｎ", ["Icon size"], []));
    }
}
