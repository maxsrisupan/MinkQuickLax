using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ManualBuilder;

/// <summary>
/// The manual sources (PLAN.md section 2): <c>th/*.md</c> and <c>en/*.md</c> chapters, <c>keywords.json</c> with extra search
/// words per section id, and <c>template/</c>. Loading also checks the ids (SPEC 4.9): every heading has one, none repeat,
/// links and keywords point at real ones, and the ids the app opens exist.
/// </summary>
public sealed partial class ManualProject
{
    public const string PrimaryLanguage = "th";
    public static readonly IReadOnlyList<string> Languages = [PrimaryLanguage, "en"];

    private readonly List<Diagnostic> _diagnostics = [];

    private ManualProject(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    /// <summary>Chapters per language in reading order. English falls back to the Thai chapter where no translation exists.</summary>
    public Dictionary<string, IReadOnlyList<Chapter>> Chapters { get; } = [];

    /// <summary>Extra search words, keyed by section id, used in both languages.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Keywords { get; private set; } = new Dictionary<string, IReadOnlyList<string>>();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public bool HasErrors => _diagnostics.Any(d => d.Severity == Severity.Error);

    /// <param name="topicsFile">C# file whose <c>const string</c> values are the section ids the app opens directly.</param>
    public static ManualProject Load(string directory, string? topicsFile)
    {
        var project = new ManualProject(directory);
        project.LoadChapters();
        project.LoadKeywords();
        if (topicsFile is not null)
        {
            project.CheckTopics(topicsFile);
        }
        return project;
    }

    /// <summary>Section ids of a language, in reading order.</summary>
    public IEnumerable<string> Ids(string language) =>
        Chapters[language].SelectMany(c => c.Headings).Select(h => h.Id).Where(id => id.Length > 0);

    private void LoadChapters()
    {
        var primary = LoadLanguage(PrimaryLanguage);
        if (primary.Count == 0)
        {
            Error(PrimaryLanguage, 0, "MANUAL001", "No chapters found; the Thai manual is required.");
        }
        Chapters[PrimaryLanguage] = primary;
        CheckIds(primary);

        var english = LoadLanguage("en").ToDictionary(c => c.FileName, StringComparer.OrdinalIgnoreCase);
        var merged = new List<Chapter>();
        foreach (var thai in primary)
        {
            if (english.Remove(thai.FileName, out var translated))
            {
                merged.Add(translated);
                CompareWithPrimary(thai, translated);
            }
            else
            {
                merged.Add(Chapter.Load(Directory, "en", Path.Combine(Directory, PrimaryLanguage, thai.FileName), isFallback: true));
                Note($"en/{thai.FileName}", 0, "MANUAL101", "Not translated yet; the English manual shows the Thai chapter.");
            }
        }
        foreach (var extra in english.Values)
        {
            Error(extra.RelativePath, 0, "MANUAL002", "This chapter has no Thai version; add th/" + extra.FileName + " or remove it.");
        }
        Chapters["en"] = merged;
        CheckIds([.. merged.Where(c => !c.IsFallback)]);
        CheckLinks();
    }

    private List<Chapter> LoadLanguage(string language)
    {
        var folder = Path.Combine(Directory, language);
        if (!System.IO.Directory.Exists(folder))
        {
            return [];
        }
        return
        [
            .. System.IO.Directory.EnumerateFiles(folder, "*.md")
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(path => Chapter.Load(Directory, language, path)),
        ];
    }

    private void CheckIds(IReadOnlyList<Chapter> chapters)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chapter in chapters)
        {
            if (chapter.Headings.Count(h => h.Level == 1) != 1)
            {
                Error(chapter.RelativePath, 1, "MANUAL003", "A chapter needs exactly one level-1 heading.");
            }
            foreach (var heading in chapter.Headings)
            {
                if (heading.Id.Length == 0)
                {
                    Error(chapter.RelativePath, heading.Line, "MANUAL004", $"Heading \"{heading.Title}\" needs an id, for example {{#settings-icon-size}}.");
                    continue;
                }
                if (!IdPattern().IsMatch(heading.Id))
                {
                    Error(chapter.RelativePath, heading.Line, "MANUAL005", $"Id \"{heading.Id}\" must be lowercase English words joined by '-'.");
                }
                if (!seen.TryAdd(heading.Id, chapter.RelativePath))
                {
                    Error(chapter.RelativePath, heading.Line, "MANUAL006", $"Id \"{heading.Id}\" is already used in {seen[heading.Id]}.");
                }
            }
        }
    }

    /// <summary>A translation must keep the section ids, so switching language lands on the same section.</summary>
    private void CompareWithPrimary(Chapter thai, Chapter translated)
    {
        var thaiIds = thai.Headings.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        var englishIds = translated.Headings.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var missing in thaiIds.Except(englishIds).Where(id => id.Length > 0))
        {
            Note(translated.RelativePath, 0, "MANUAL102", $"Section \"{missing}\" is not translated yet.");
        }
        foreach (var heading in translated.Headings.Where(h => h.Id.Length > 0 && !thaiIds.Contains(h.Id)))
        {
            Error(translated.RelativePath, heading.Line, "MANUAL007", $"Section \"{heading.Id}\" does not exist in th/{thai.FileName}; both languages use the same ids.");
        }
    }

    private void CheckLinks()
    {
        foreach (var language in Languages)
        {
            var ids = Ids(language).ToHashSet(StringComparer.Ordinal);
            foreach (var chapter in Chapters[language].Where(c => !c.IsFallback))
            {
                foreach (var (id, line) in chapter.AnchorLinks())
                {
                    if (!ids.Contains(id))
                    {
                        Error(chapter.RelativePath, line, "MANUAL008", $"Link to \"#{id}\" goes nowhere.");
                    }
                }
            }
        }
    }

    private void LoadKeywords()
    {
        var path = Path.Combine(Directory, "keywords.json");
        if (!File.Exists(path))
        {
            return;
        }
        Dictionary<string, List<string>>? keywords;
        try
        {
            keywords = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (JsonException ex)
        {
            Error("keywords.json", (int)(ex.LineNumber ?? 0) + 1, "MANUAL009", ex.Message);
            return;
        }
        keywords ??= [];
        var ids = Ids(PrimaryLanguage).ToHashSet(StringComparer.Ordinal);
        foreach (var id in keywords.Keys.Where(id => !ids.Contains(id)))
        {
            Error("keywords.json", 0, "MANUAL010", $"\"{id}\" is not a section id.");
        }
        Keywords = keywords.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.Ordinal);
    }

    private void CheckTopics(string topicsFile)
    {
        if (!File.Exists(topicsFile))
        {
            Error(topicsFile, 0, "MANUAL011", "The file with the app's manual topics was not found.");
            return;
        }
        var ids = Ids(PrimaryLanguage).ToHashSet(StringComparer.Ordinal);
        var lines = File.ReadAllLines(topicsFile, Encoding.UTF8);
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (Match match in TopicConstant().Matches(lines[i]))
            {
                if (!ids.Contains(match.Groups[1].Value))
                {
                    Error(topicsFile, i + 1, "MANUAL012", $"The app opens the manual at \"{match.Groups[1].Value}\", which no Thai chapter has.");
                }
            }
        }
    }

    private void Error(string file, int line, string code, string message) => _diagnostics.Add(new Diagnostic(Severity.Error, file, line, code, message));

    private void Note(string file, int line, string code, string message) => _diagnostics.Add(new Diagnostic(Severity.Note, file, line, code, message));

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"const\s+string\s+\w+\s*=\s*""([^""]+)""")]
    private static partial Regex TopicConstant();
}
