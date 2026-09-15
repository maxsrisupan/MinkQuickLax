using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ManualBuilder;

/// <summary>
/// Writes one self-contained HTML file per language (SPEC 4.9): the template with its CSS, JS, fonts and images inlined, the
/// table of contents, the chapters and the search index. Nothing is loaded from the network.
/// </summary>
public sealed partial class ManualWriter(ManualProject project, string fontsDirectory)
{
    /// <summary>Embedded faces: name, file, weight. Monospace text falls back to Consolas.</summary>
    private static readonly (string Family, string File, int Weight)[] Fonts =
    [
        ("IBM Plex Sans Thai", "IBMPlexSansThai-Regular.ttf", 400),
        ("IBM Plex Sans Thai", "IBMPlexSansThai-SemiBold.ttf", 600),
        ("Chakra Petch", "ChakraPetch-SemiBold.ttf", 600),
    ];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public IReadOnlyList<string> Write(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var templateDirectory = Path.Combine(project.Directory, "template");
        var template = File.ReadAllText(Path.Combine(templateDirectory, "index.html"), Encoding.UTF8);
        var css = File.ReadAllText(Path.Combine(templateDirectory, "manual.css"), Encoding.UTF8);
        var script = File.ReadAllText(Path.Combine(templateDirectory, "search.js"), Encoding.UTF8);
        var strings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
            File.ReadAllText(Path.Combine(templateDirectory, "strings.json"), Encoding.UTF8)) ?? [];
        var fontFaces = FontFaces();

        var written = new List<string>();
        foreach (var language in ManualProject.Languages)
        {
            var text = strings[language];
            var chapters = project.Chapters[language];
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lang"] = language,
                ["otherLang"] = language == "th" ? "en" : "th",
                ["fontFaces"] = fontFaces,
                ["css"] = css,
                ["script"] = script,
                ["toc"] = Toc(chapters),
                ["content"] = Content(chapters, text),
                ["index"] = SearchIndex(chapters),
                ["strings"] = JsonSerializer.Serialize(text, Json),
            };
            foreach (var (key, value) in text)
            {
                values["s:" + key] = WebUtility.HtmlEncode(value);
            }
            var html = Token().Replace(template, match =>
                values.TryGetValue(match.Groups[1].Value, out var value) ? value : throw new InvalidOperationException($"Unknown template token {match.Value}"));
            var path = Path.Combine(outputDirectory, language + ".html");
            // Only touch the file when it changes, so incremental builds stay incremental.
            if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8) != html)
            {
                File.WriteAllText(path, html, new UTF8Encoding(false));
            }
            written.Add(path);
        }
        return written;
    }

    private static string Toc(IReadOnlyList<Chapter> chapters)
    {
        var html = new StringBuilder("<ol class=\"toc-chapters\">");
        foreach (var chapter in chapters)
        {
            if (chapter.Title is not { } title)
            {
                continue;
            }
            html.Append(CultureInfo.InvariantCulture, $"<li><a href=\"#{title.Id}\">{WebUtility.HtmlEncode(title.Title)}</a>");
            var sections = chapter.Headings.Where(h => h.Level == 2).ToList();
            if (sections.Count > 0)
            {
                html.Append("<ol>");
                foreach (var section in sections)
                {
                    html.Append(CultureInfo.InvariantCulture, $"<li><a href=\"#{section.Id}\">{WebUtility.HtmlEncode(section.Title)}</a></li>");
                }
                html.Append("</ol>");
            }
            html.Append("</li>");
        }
        return html.Append("</ol>").ToString();
    }

    private string Content(IReadOnlyList<Chapter> chapters, Dictionary<string, string> text)
    {
        var html = new StringBuilder();
        foreach (var chapter in chapters)
        {
            InlineImages(chapter);
            html.Append("<article class=\"chapter\">");
            var writer = new StringWriter(CultureInfo.InvariantCulture);
            var renderer = new HtmlRenderer(writer);
            Chapter.Pipeline.Setup(renderer);
            renderer.Render(chapter.Document);
            writer.Flush();
            var body = writer.ToString();
            if (chapter.IsFallback)
            {
                // The notice goes right after the chapter title.
                var titleEnd = body.IndexOf("</h1>", StringComparison.Ordinal);
                var notice = $"<p class=\"untranslated\">{WebUtility.HtmlEncode(text["untranslated"])}</p>";
                body = titleEnd < 0 ? notice + body : body.Insert(titleEnd + 5, notice);
            }
            html.Append(body).Append("</article>");
        }
        return html.ToString();
    }

    /// <summary>Images become data URIs so the manual stays one file (paths are relative to the Markdown file).</summary>
    private void InlineImages(Chapter chapter)
    {
        var folder = Path.GetDirectoryName(Path.Combine(project.Directory, chapter.IsFallback ? ManualProject.PrimaryLanguage + "/" + chapter.FileName : chapter.RelativePath))!;
        foreach (var image in chapter.Document.Descendants<LinkInline>().Where(l => l.IsImage && l.Url is { } url && !url.StartsWith("data:", StringComparison.Ordinal)))
        {
            var path = Path.GetFullPath(Path.Combine(folder, image.Url!));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"{chapter.RelativePath}({image.Line + 1}): image not found", path);
            }
            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "application/octet-stream",
            };
            image.Url = $"data:{type};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        }
    }

    private string SearchIndex(IReadOnlyList<Chapter> chapters)
    {
        var entries = chapters
            .SelectMany(c => c.Sections())
            .Where(s => s.Id.Length > 0)
            .Select(s => new
            {
                id = s.Id,
                title = s.Title,
                chapter = s.Chapter,
                text = s.Text,
                keywords = project.Keywords.TryGetValue(s.Id, out var words) ? words : [],
            });
        return JsonSerializer.Serialize(entries, Json);
    }

    private string FontFaces()
    {
        var css = new StringBuilder();
        foreach (var (family, file, weight) in Fonts)
        {
            var data = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(fontsDirectory, file)));
            css.Append(CultureInfo.InvariantCulture, $"@font-face{{font-family:\"{family}\";font-weight:{weight};font-style:normal;font-display:block;src:url(data:font/ttf;base64,{data}) format(\"truetype\")}}\n");
        }
        return css.ToString();
    }

    [GeneratedRegex(@"\{\{([a-zA-Z:]+)\}\}")]
    private static partial Regex Token();
}
