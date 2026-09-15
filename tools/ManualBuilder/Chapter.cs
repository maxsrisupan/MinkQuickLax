using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ManualBuilder;

/// <param name="Level">1 for the chapter title, 2 and 3 for sections.</param>
/// <param name="Id">The explicit <c>{#id}</c>; the same in every language so switching language keeps the place.</param>
public sealed record Heading(int Level, string Id, string Title, int Line);

/// <summary>A searchable piece of the manual: one heading and the text up to the next heading.</summary>
public sealed record Section(string Id, string Title, string Chapter, string Text);

/// <summary>One Markdown file of the manual, parsed.</summary>
public sealed class Chapter
{
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseListExtras()
        .UseAutoLinks()
        // Must come last so {#id} after a heading is read as an attribute.
        .UseGenericAttributes()
        .Build();

    private Chapter(string language, string fileName, string relativePath, MarkdownDocument document, bool isFallback)
    {
        Language = language;
        FileName = fileName;
        RelativePath = relativePath;
        Document = document;
        IsFallback = isFallback;
        Headings =
        [
            .. document.Descendants<HeadingBlock>()
                .Where(h => h.Level <= 3)
                .Select(h => new Heading(h.Level, h.GetAttributes().Id ?? "", InlineText(h.Inline), h.Line + 1)),
        ];
    }

    public string Language { get; }

    /// <summary>For example <c>01-getting-started.md</c>; chapters are ordered by it and matched across languages by it.</summary>
    public string FileName { get; }

    /// <summary>Path relative to the manual folder, for messages.</summary>
    public string RelativePath { get; }

    public MarkdownDocument Document { get; }

    /// <summary>The Thai chapter shown in the English manual because the translation does not exist yet.</summary>
    public bool IsFallback { get; }

    public IReadOnlyList<Heading> Headings { get; }

    public Heading? Title => Headings.FirstOrDefault(h => h.Level == 1);

    public static Chapter Load(string manualDirectory, string language, string path, bool isFallback = false)
    {
        var markdown = File.ReadAllText(path, Encoding.UTF8);
        var document = Markdown.Parse(markdown, Pipeline);
        var relative = Path.GetRelativePath(manualDirectory, path).Replace('\\', '/');
        return new Chapter(language, Path.GetFileName(path), relative, document, isFallback);
    }

    /// <summary>Splits the chapter at every heading; each part keeps its plain text for search.</summary>
    public IEnumerable<Section> Sections()
    {
        var chapterTitle = Title?.Title ?? "";
        HeadingBlock? current = null;
        var text = new StringBuilder();
        foreach (var block in Document)
        {
            if (block is HeadingBlock heading)
            {
                if (current is not null)
                {
                    yield return MakeSection(current, chapterTitle, text);
                }
                current = heading;
                text.Clear();
                continue;
            }
            AppendText(block, text);
        }
        if (current is not null)
        {
            yield return MakeSection(current, chapterTitle, text);
        }
    }

    /// <summary>Every <c>[text](#id)</c> link, with its line, so broken references can be reported.</summary>
    public IEnumerable<(string Id, int Line)> AnchorLinks() =>
        Document.Descendants<LinkInline>()
            .Where(link => !link.IsImage && link.Url is { } url && url.StartsWith('#'))
            .Select(link => (link.Url![1..], link.Line + 1));

    public static string InlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return "";
        }
        var text = new StringBuilder();
        foreach (var item in inline.Descendants())
        {
            switch (item)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
            }
        }
        return text.ToString().Trim();
    }

    private static Section MakeSection(HeadingBlock heading, string chapterTitle, StringBuilder text) =>
        new(heading.GetAttributes().Id ?? "", InlineText(heading.Inline), chapterTitle, Collapse(text.ToString()));

    private static void AppendText(Block block, StringBuilder text)
    {
        switch (block)
        {
            case LeafBlock { Inline: { } inline }:
                text.Append(InlineText(inline)).Append(' ');
                break;
            case CodeBlock code:
                text.Append(code.Lines.ToString()).Append(' ');
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AppendText(child, text);
                }
                break;
        }
    }

    private static string Collapse(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
