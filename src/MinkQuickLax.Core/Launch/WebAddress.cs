using System.Net;
using System.Text.RegularExpressions;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Launch;

/// <summary>
/// Reads what the user types into "Add website" (SPEC 4.1). Everything typed there is meant as a web address,
/// so a bare <c>youtube.com</c> gets a scheme instead of being taken for a file.
/// </summary>
public static partial class WebAddress
{
    /// <summary>The web link for <paramref name="input"/>, or null when it cannot be opened as an address.</summary>
    public static DetectedLink? Parse(string? input)
    {
        var text = (input ?? "").Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1].Trim();
        }
        if (text.Length == 0 || text.Any(char.IsWhiteSpace))
        {
            return null;
        }
        if (!Scheme().IsMatch(text))
        {
            var host = HostOf(text);
            if (!LooksLikeHost(host))
            {
                return null;
            }
            // Browsers treat local servers and bare IP addresses as plain http; everything else is https today.
            text = (IsLocal(host) ? "http://" : "https://") + text;
        }

        var detected = LinkKindDetector.Detect(text, NoPaths.Instance);
        if (detected?.Kind is not (LinkKind.Url or LinkKind.MsSettings))
        {
            return null;
        }
        // "https:" on its own parses, but goes nowhere.
        return IsHttp(detected.Target) && new Uri(detected.Target).Host.Length == 0 ? null : detected;
    }

    /// <summary>Whether <paramref name="target"/> is an http or https address, the only kind a browser is chosen for and a favicon fetched from.</summary>
    public static bool IsHttp(string? target) =>
        Uri.TryCreate(target, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string HostOf(string text)
    {
        var end = text.IndexOfAny(['/', '?', '#', '\\']);
        var host = end < 0 ? text : text[..end];
        var port = PortSuffix().Match(host);
        return port.Success ? host[..port.Index] : host;
    }

    private static bool LooksLikeHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (host.Contains('.', StringComparison.Ordinal) && !host.StartsWith('.') && !host.EndsWith('.') && !host.Contains("..", StringComparison.Ordinal));

    private static bool IsLocal(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(host, out _) && host.Count(c => c == '.') == 3);

    /// <summary>A scheme such as <c>https:</c>, <c>mailto:</c> or <c>ms-settings:</c>, but not <c>youtube.com:8080</c> or <c>localhost:3000</c>.</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+\-]*:(?!\d)")]
    private static partial Regex Scheme();

    [GeneratedRegex(@":\d+$")]
    private static partial Regex PortSuffix();

    /// <summary>Nothing typed here is a file, so detection never looks at the disk.</summary>
    private sealed class NoPaths : IPathProbe
    {
        public static NoPaths Instance { get; } = new();

        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => false;
    }
}
