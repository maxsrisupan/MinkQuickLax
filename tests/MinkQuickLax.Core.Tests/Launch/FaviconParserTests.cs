using MinkQuickLax.Core.Launch;

namespace MinkQuickLax.Core.Tests.Launch;

public sealed class FaviconParserTests
{
    private static readonly Uri Page = new("https://example.com/docs/page.html");

    [Fact]
    public void NoHtml_FallsBackToFaviconIco()
    {
        Assert.Equal([new Uri("https://example.com/favicon.ico")], FaviconParser.Candidates(null, Page));
    }

    [Fact]
    public void PrefersTheLargestDeclaredIcon()
    {
        const string Html = """
            <html><head>
              <link rel="icon" href="/small.png" sizes="16x16">
              <link rel='icon' href='big.png' sizes='192x192'>
              <link rel="apple-touch-icon" href="https://cdn.example.com/touch.png">
              <link rel="stylesheet" href="/site.css">
            </head></html>
            """;

        var candidates = FaviconParser.Candidates(Html, Page);

        Assert.Equal(
        [
            new Uri("https://example.com/docs/big.png"),
            new Uri("https://cdn.example.com/touch.png"),
            new Uri("https://example.com/small.png"),
            new Uri("https://example.com/favicon.ico"),
        ], candidates);
    }

    [Fact]
    public void ShortcutIconAndBaseTag_AreUnderstood()
    {
        const string Html = """<head><base href="https://static.example.org/assets/"><LINK REL="shortcut icon" HREF="fav.ico"></head>""";

        var candidates = FaviconParser.Candidates(Html, Page);

        Assert.Equal(new Uri("https://static.example.org/assets/fav.ico"), candidates[0]);
    }

    [Fact]
    public void SvgAndNonHttpIcons_AreSkipped()
    {
        const string Html = """
            <link rel="icon" href="/logo.svg" type="image/svg+xml">
            <link rel="icon" href="data:image/png;base64,AAAA">
            <link rel="icon" href="/real.png">
            """;

        var candidates = FaviconParser.Candidates(Html, Page);

        Assert.Equal([new Uri("https://example.com/real.png"), new Uri("https://example.com/favicon.ico")], candidates);
    }

    [Fact]
    public void HtmlEntitiesInHref_AreDecoded()
    {
        var candidates = FaviconParser.Candidates("""<link rel="icon" href="/i.png?a=1&amp;b=2">""", Page);

        Assert.Equal(new Uri("https://example.com/i.png?a=1&b=2"), candidates[0]);
    }
}
