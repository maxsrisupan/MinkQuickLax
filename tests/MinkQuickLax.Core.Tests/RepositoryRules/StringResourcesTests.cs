using System.Xml.Linq;

namespace MinkQuickLax.Core.Tests.RepositoryRules;

/// <summary>
/// Every user-visible string must exist in both English and Thai (PLAN.md section 5).
/// Reads the .resx files directly so the check needs no WPF reference.
/// </summary>
public sealed class StringResourcesTests
{
    private static readonly string EnglishPath = RepoPaths.Combine("src", "MinkQuickLax", "Resources", "Strings.resx");
    private static readonly string ThaiPath = RepoPaths.Combine("src", "MinkQuickLax", "Resources", "Strings.th.resx");

    [Fact]
    public void ThaiAndEnglish_HaveTheSameKeys()
    {
        var english = LoadStrings(EnglishPath);
        var thai = LoadStrings(ThaiPath);

        Assert.Empty(english.Keys.Except(thai.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Empty(thai.Keys.Except(english.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ResourceFiles))]
    public void EveryString_HasAValue(string path)
    {
        var blank = LoadStrings(path).Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key);

        Assert.Empty(blank);
    }

    public static TheoryData<string> ResourceFiles => new(EnglishPath, ThaiPath);

    private static Dictionary<string, string> LoadStrings(string path) =>
        XDocument.Load(path).Root!
            .Elements("data")
            .Where(data => data.Attribute("type") is null)
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);
}
