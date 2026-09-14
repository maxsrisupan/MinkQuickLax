namespace MinkQuickLax.Core.Launch;

/// <summary>String handling for opening a web link in a chosen browser (SPEC 4.1).</summary>
public static class BrowserCommand
{
    /// <summary>The program in a registered "open" command such as <c>"C:\Program Files\…\chrome.exe" --flag</c>.</summary>
    public static string? ExecutablePath(string? command)
    {
        var text = command?.Trim() ?? "";
        if (text.Length == 0)
        {
            return null;
        }
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }
        // Unquoted: everything up to ".exe" (paths may contain spaces), or up to the first space.
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe > 0)
        {
            return text[..(exe + 4)];
        }
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space > 0 ? text[..space] : text;
    }

    /// <summary>
    /// The single argument that hands <paramref name="url"/> to a browser, or null when it is not a plain web address.
    /// Only http and https are passed on, so a link can never smuggle extra command-line switches into the browser.
    /// </summary>
    public static string? UrlArgument(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }
        // AbsoluteUri percent-encodes quotes and spaces, so wrapping it in quotes keeps it one argument.
        return "\"" + uri.AbsoluteUri + "\"";
    }
}
