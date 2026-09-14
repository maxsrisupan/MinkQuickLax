using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Launch;

/// <summary>File system checks, so detection can be tested without real files.</summary>
public interface IPathProbe
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
}

public sealed class SystemPathProbe : IPathProbe
{
    public static SystemPathProbe Instance { get; } = new();

    public bool FileExists(string path) => File.Exists(Environment.ExpandEnvironmentVariables(path));

    public bool DirectoryExists(string path) => Directory.Exists(Environment.ExpandEnvironmentVariables(path));
}

/// <param name="Target">The cleaned-up target to store (quotes removed, AppsFolder prefix stripped for shell apps).</param>
/// <param name="SuggestedName">A name to prefill: file name, folder name or web host.</param>
public sealed record DetectedLink(LinkKind Kind, string Target, string SuggestedName);

/// <summary>Works out what kind of link a pasted path, URL or AppsFolder id is.</summary>
public static class LinkKindDetector
{
    private const string AppsFolderPrefix = "shell:AppsFolder\\";

    private static readonly HashSet<string> AppExtensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk", ".appref-ms" };
    private static readonly HashSet<string> CommandExtensions = new(StringComparer.OrdinalIgnoreCase) { ".bat", ".cmd", ".ps1", ".vbs", ".wsf" };

    public static DetectedLink? Detect(string? input, IPathProbe probe)
    {
        var text = Clean(input);
        if (text.Length == 0)
        {
            return null;
        }

        if (text.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
        {
            return new DetectedLink(LinkKind.MsSettings, text, text["ms-settings:".Length..]);
        }
        if (text.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var id = text[AppsFolderPrefix.Length..];
            return id.Length == 0 ? null : new DetectedLink(LinkKind.ShellApp, id, AppIdName(id));
        }
        if (LooksLikeAppUserModelId(text))
        {
            return new DetectedLink(LinkKind.ShellApp, text, AppIdName(text));
        }
        if (TryUrl(text, out var url))
        {
            return url;
        }
        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && !text.Contains('\\', StringComparison.Ordinal))
        {
            return TryUrl("https://" + text, out url) ? url : null;
        }

        if (probe.DirectoryExists(text))
        {
            return new DetectedLink(LinkKind.Folder, text, FolderName(text));
        }
        var extension = Path.GetExtension(text);
        var name = Path.GetFileNameWithoutExtension(text.TrimEnd('\\', '/'));
        if (!probe.FileExists(text) && extension.Length == 0)
        {
            // Nothing there and no extension: most likely a folder that is not available right now.
            return text.EndsWith('\\') || text.EndsWith('/') || IsDriveRoot(text)
                ? new DetectedLink(LinkKind.Folder, text, FolderName(text))
                : new DetectedLink(LinkKind.File, text, name);
        }
        if (AppExtensions.Contains(extension))
        {
            return new DetectedLink(LinkKind.App, text, name);
        }
        if (CommandExtensions.Contains(extension))
        {
            return new DetectedLink(LinkKind.Command, text, name);
        }
        return new DetectedLink(LinkKind.File, text, name);
    }

    private static string Clean(string? input)
    {
        var text = (input ?? "").Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1].Trim();
        }
        return text;
    }

    /// <summary>Packaged app ids look like <c>Publisher.App_hash!Entry</c>.</summary>
    private static bool LooksLikeAppUserModelId(string text)
    {
        var bang = text.IndexOf('!', StringComparison.Ordinal);
        return bang > 0 && bang < text.Length - 1
            && text.IndexOfAny(['\\', '/', ':', ' ']) < 0
            && text.AsSpan(0, bang).Contains('_');
    }

    private static bool TryUrl(string text, out DetectedLink? link)
    {
        link = null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.IsFile || uri.IsUnc)
        {
            return false;
        }
        // "C:\x" parses as scheme "c"; a real scheme is longer than one letter.
        if (uri.Scheme.Length < 2)
        {
            return false;
        }
        var name = uri.Scheme is "http" or "https" && uri.Host.Length > 0
            ? (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host)
            : uri.Scheme;
        link = new DetectedLink(LinkKind.Url, text, name);
        return true;
    }

    private static string AppIdName(string id)
    {
        var package = id.Split('!')[0];
        var family = package.Split('_')[0];
        var dot = family.LastIndexOf('.');
        return dot >= 0 && dot < family.Length - 1 ? family[(dot + 1)..] : family;
    }

    private static string FolderName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var name = Path.GetFileName(trimmed);
        return name.Length > 0 ? name : trimmed;
    }

    private static bool IsDriveRoot(string text) => text.Length == 2 && text[1] == ':' && char.IsAsciiLetter(text[0]);
}
