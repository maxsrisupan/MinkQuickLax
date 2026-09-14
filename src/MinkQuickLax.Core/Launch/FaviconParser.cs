using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace MinkQuickLax.Core.Launch;

/// <summary>Finds icon links in a page's HTML, best first (SPEC 4.1: favicon straight from the site).</summary>
public static partial class FaviconParser
{
    /// <summary>Icon URLs declared by the page, ordered by preference, then <c>/favicon.ico</c> as the last resort.</summary>
    public static IReadOnlyList<Uri> Candidates(string? html, Uri page)
    {
        var baseUri = page;
        var declared = new List<(Uri Url, int Score)>();
        if (!string.IsNullOrEmpty(html))
        {
            // Only the head matters and pages can be large.
            var head = html.Length > 200_000 ? html[..200_000] : html;
            if (BaseTag().Match(head) is { Success: true } baseMatch && Attribute(baseMatch.Value, "href") is { } baseHref
                && Uri.TryCreate(page, baseHref, out var parsedBase))
            {
                baseUri = parsedBase;
            }
            foreach (Match link in LinkTag().Matches(head))
            {
                var tag = link.Value;
                var rel = Attribute(tag, "rel")?.ToLowerInvariant();
                var href = Attribute(tag, "href");
                if (rel is null || string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(baseUri, href, out var url) || url.Scheme is not ("http" or "https"))
                {
                    continue;
                }
                var relParts = rel.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (!relParts.Contains("icon") && !relParts.Contains("apple-touch-icon") && !relParts.Contains("apple-touch-icon-precomposed"))
                {
                    continue;
                }
                var type = Attribute(tag, "type")?.ToLowerInvariant() ?? "";
                if (type.Contains("svg", StringComparison.Ordinal) || url.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    // SVG icons arrive in phase 2.
                    continue;
                }
                declared.Add((url, Score(relParts, Attribute(tag, "sizes"))));
            }
        }

        var ordered = declared
            .OrderByDescending(d => d.Score)
            .Select(d => d.Url)
            .ToList();
        var fallback = new Uri(page, "/favicon.ico");
        if (!ordered.Contains(fallback))
        {
            ordered.Add(fallback);
        }
        return ordered.Distinct().ToList();
    }

    /// <summary>Bigger is better up to about 256 px; apple-touch icons are usually 180 px and a good fallback.</summary>
    private static int Score(string[] rel, string? sizes)
    {
        var largest = 0;
        foreach (var size in (sizes ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (size.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                largest = Math.Max(largest, 256);
                continue;
            }
            var parts = size.ToLowerInvariant().Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var w))
            {
                largest = Math.Max(largest, w);
            }
        }
        if (largest == 0)
        {
            largest = rel.Contains("apple-touch-icon") || rel.Contains("apple-touch-icon-precomposed") ? 180 : 32;
        }
        // Prefer sizes close to 256 without going far beyond it.
        return largest > 512 ? 256 : largest;
    }

    private static string? Attribute(string tag, string name)
    {
        foreach (Match m in AttributePattern().Matches(tag))
        {
            if (m.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var value = m.Groups["dq"].Success ? m.Groups["dq"].Value : m.Groups["sq"].Success ? m.Groups["sq"].Value : m.Groups["bare"].Value;
                return WebUtility.HtmlDecode(value).Trim();
            }
        }
        return null;
    }

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkTag();

    [GeneratedRegex(@"<base\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BaseTag();

    [GeneratedRegex(@"(?<name>[a-zA-Z_:][-a-zA-Z0-9_:.]*)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s""'>]+))", RegexOptions.CultureInvariant)]
    private static partial Regex AttributePattern();
}
