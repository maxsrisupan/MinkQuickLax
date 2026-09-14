using Microsoft.Win32;
using MinkQuickLax.Core.Launch;
using Windows.Win32;

namespace MinkQuickLax.Platform.Shell;

/// <param name="Id">The key name under StartMenuInternet, stored in <c>Link.Browser</c>.</param>
/// <param name="Name">The name Windows shows for the browser.</param>
public sealed record BrowserInfo(string Id, string Name, string ExePath);

/// <summary>
/// The browsers Windows offers in its default-apps page, read from StartMenuInternet for the user and the machine
/// (SPEC 4.1). Read fresh each time: browsers come and go.
/// </summary>
public static unsafe class BrowserCatalog
{
    private const string ClientsKey = @"SOFTWARE\Clients\StartMenuInternet";

    // Internet Explorer is retired and only forwards to Edge.
    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase) { "IEXPLORE.EXE" };

    public static IReadOnlyList<BrowserInfo> List()
    {
        var found = new Dictionary<string, BrowserInfo>(StringComparer.OrdinalIgnoreCase);
        // The user's own registration wins over the machine's.
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var clients = root.OpenSubKey(ClientsKey);
            if (clients is null)
            {
                continue;
            }
            foreach (var id in clients.GetSubKeyNames())
            {
                if (!Hidden.Contains(id) && !found.ContainsKey(id) && Read(clients, id) is { } browser)
                {
                    found[id] = browser;
                }
            }
        }
        return [.. found.Values.OrderBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public static BrowserInfo? Find(string? id) =>
        string.IsNullOrEmpty(id) ? null : List().FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));

    private static BrowserInfo? Read(RegistryKey clients, string id)
    {
        using var key = clients.OpenSubKey(id);
        using var command = key?.OpenSubKey(@"shell\open\command");
        var exe = BrowserCommand.ExecutablePath(command?.GetValue(null) as string);
        if (key is null || exe is null || !File.Exists(Environment.ExpandEnvironmentVariables(exe)))
        {
            return null;
        }
        using var capabilities = key.OpenSubKey("Capabilities");
        var name = FirstReadable(capabilities?.GetValue("ApplicationName") as string, key.GetValue(null) as string) ?? id;
        return new BrowserInfo(id, name, Environment.ExpandEnvironmentVariables(exe));
    }

    /// <summary>Names can be "@file.dll,-123" resource references; those are resolved, and unresolvable ones skipped.</summary>
    private static string? FirstReadable(params string?[] candidates)
    {
        var buffer = stackalloc char[260];
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }
            if (!candidate.StartsWith('@'))
            {
                return candidate;
            }
            fixed (char* source = candidate)
            {
                if (PInvoke.SHLoadIndirectString(source, buffer, 260, null).Succeeded)
                {
                    return new string(buffer);
                }
            }
        }
        return null;
    }
}
