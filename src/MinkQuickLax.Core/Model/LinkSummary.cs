namespace MinkQuickLax.Core.Model;

/// <summary>Short technical descriptions of a link, for the meta line of the HUD and Dot Matrix side labels (SPEC 5.8, 5.9).</summary>
public static class LinkSummary
{
    /// <summary>
    /// The program or file name, the folder name, the web host or the settings page — whatever tells links of the same
    /// kind apart. Empty for installed Store apps, whose target is an opaque id.
    /// </summary>
    public static string Subject(Link link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var target = link.Target.Trim();
        switch (link.Kind)
        {
            case LinkKind.ShellApp:
                return "";
            case LinkKind.Url:
                return Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : target;
            case LinkKind.MsSettings:
                var colon = target.IndexOf(':', StringComparison.Ordinal);
                return colon >= 0 ? target[(colon + 1)..] : target;
            default:
                var trimmed = target.TrimEnd('\\', '/');
                var name = Path.GetFileName(trimmed);
                return name.Length > 0 ? name : target;
        }
    }
}
