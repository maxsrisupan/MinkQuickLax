using System.Globalization;

namespace MinkQuickLax.Core.Model;

/// <summary>
/// Substring search that works for Thai, which has no spaces between words (SPEC 4.8, 4.9): a query matches
/// when it appears anywhere in a name or a keyword, ignoring case and width.
/// </summary>
public static class TextSearch
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType;

    /// <summary>0 when nothing matches; higher is better: a name that starts with the query, then a name that contains it, then a keyword.</summary>
    public static int Score(string query, IEnumerable<string> names, IEnumerable<string> keywords)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(keywords);
        var needle = query?.Trim() ?? "";
        if (needle.Length == 0)
        {
            return 0;
        }

        var compare = CultureInfo.InvariantCulture.CompareInfo;
        var best = 0;
        foreach (var name in names)
        {
            var at = compare.IndexOf(name, needle, Options);
            if (at == 0)
            {
                return 3;
            }
            if (at > 0)
            {
                best = 2;
            }
        }
        if (best > 0)
        {
            return best;
        }
        return keywords.Any(k => compare.IndexOf(k, needle, Options) >= 0) ? 1 : 0;
    }
}
