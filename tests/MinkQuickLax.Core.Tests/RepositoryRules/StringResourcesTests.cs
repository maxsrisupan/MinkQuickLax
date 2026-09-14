using System.Text.RegularExpressions;
using System.Xml.Linq;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.RepositoryRules;

/// <summary>
/// Every user-visible string must exist in both English and Thai (PLAN.md section 5, M9).
/// Reads the .resx files and the app's source directly so the check needs no WPF reference.
/// </summary>
public sealed partial class StringResourcesTests
{
    private static readonly string AppPath = RepoPaths.Combine("src", "MinkQuickLax");
    private static readonly string EnglishPath = Path.Combine(AppPath, "Resources", "Strings.resx");
    private static readonly string ThaiPath = Path.Combine(AppPath, "Resources", "Strings.th.resx");

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

    /// <summary>A translation that drops or adds a <c>{0}</c> would show the wrong value or throw at run time.</summary>
    [Fact]
    public void Placeholders_AreTheSameInBothLanguages()
    {
        var thai = LoadStrings(ThaiPath);
        var mismatched = LoadStrings(EnglishPath)
            .Where(pair => thai.TryGetValue(pair.Key, out var translated) && !Placeholders(pair.Value).SetEquals(Placeholders(translated)))
            .Select(pair => pair.Key);

        Assert.Empty(mismatched);
    }

    /// <summary>Keys are looked up by name at run time; a typo would put the key itself on screen.</summary>
    [Fact]
    public void EveryKeyTheAppUses_Exists()
    {
        var keys = LoadStrings(EnglishPath).Keys.ToHashSet(StringComparer.Ordinal);
        var used = SourceFiles("*.xaml").SelectMany(file => XamlKey().Matches(File.ReadAllText(file)).Select(match => (file, key: match.Groups[1].Value)))
            .Concat(SourceFiles("*.cs").SelectMany(file => CodeKey().Matches(File.ReadAllText(file)).Select(match => (file, key: match.Groups[1].Value))))
            .ToList();
        var missing = used.Where(use => !keys.Contains(use.key)).Select(use => $"{use.key} in {Path.GetFileName(use.file)}").Distinct().Order(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(used);
        Assert.Empty(missing);
    }

    /// <summary>The settings list and the side labels build these keys from the enum name.</summary>
    [Theory]
    [InlineData("Kind_")]
    [InlineData("Meta_")]
    public void EveryLinkKind_HasAString(string prefix)
    {
        var keys = LoadStrings(EnglishPath).Keys.ToHashSet(StringComparer.Ordinal);
        var missing = Enum.GetNames<LinkKind>().Select(name => prefix + name).Where(key => !keys.Contains(key)).ToList();

        Assert.Empty(missing);
    }

    /// <summary>The settings window builds <c>Page_*</c> keys from its page enum.</summary>
    [Fact]
    public void EverySettingsPage_HasAName()
    {
        var keys = LoadStrings(EnglishPath).Keys.ToHashSet(StringComparer.Ordinal);
        var source = File.ReadAllText(Path.Combine(AppPath, "Settings", "SettingsPage.cs"));
        var pages = EnumMembers().Matches(source[source.IndexOf('{', StringComparison.Ordinal)..]).Select(match => match.Groups[1].Value).ToList();
        var missing = pages.Select(page => "Page_" + page).Where(key => !keys.Contains(key)).ToList();

        Assert.NotEmpty(pages);
        Assert.Empty(missing);
    }

    /// <summary>User-visible text in XAML must come from the resources; lone symbols such as ✕ or ↑ are fine.</summary>
    [Fact]
    public void Xaml_HasNoLiteralText()
    {
        var literals = SourceFiles("*.xaml")
            .SelectMany(file => XamlTextAttribute().Matches(File.ReadAllText(file))
                .Where(match => match.Groups[2].Value.Any(char.IsLetter))
                .Select(match => $"{Path.GetFileName(file)}: {match.Groups[1].Value}=\"{match.Groups[2].Value}\""));

        Assert.Empty(literals);
    }

    public static TheoryData<string> ResourceFiles => new(EnglishPath, ThaiPath);

    private static HashSet<string> Placeholders(string value) =>
        [.. PlaceholderPattern().Matches(value).Select(match => match.Groups[1].Value)];

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppPath, pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static Dictionary<string, string> LoadStrings(string path) =>
        XDocument.Load(path).Root!
            .Elements("data")
            .Where(data => data.Attribute("type") is null)
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{s:Text\s+([A-Za-z0-9_]+)\s*\}")]
    private static partial Regex XamlKey();

    /// <summary>String literals shaped like a key: <c>"Menu_Open"</c>. All-caps names such as environment variables are not keys.</summary>
    [GeneratedRegex(@"""([A-Z][a-z][A-Za-z0-9]*_[A-Za-z0-9_]+)""")]
    private static partial Regex CodeKey();

    [GeneratedRegex(@"^\s*([A-Z][A-Za-z0-9]*)\s*,?\s*$", RegexOptions.Multiline)]
    private static partial Regex EnumMembers();

    [GeneratedRegex(@"\b(Text|Content|Header|HeaderText|ToolTip|Title|Hint|ValueText|PlaceholderText|AutomationProperties\.Name)=""([^""{][^""]*)""")]
    private static partial Regex XamlTextAttribute();
}
