using System.Runtime.InteropServices;
using MinkQuickLax.Core.Launch;
using MinkQuickLax.Core.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace MinkQuickLax.Platform.Shell;

public enum ScanSource
{
    /// <summary>A desktop program listed in Start (All apps).</summary>
    Program,

    /// <summary>A packaged (Store/MSIX) app.</summary>
    Store,

    /// <summary>A shortcut on the desktop.</summary>
    Desktop,
}

/// <param name="IconParsingName">Shell parsing name to draw the icon exactly as Start or the desktop shows it.</param>
public sealed record ScannedApp(string Name, ScanSource Source, LinkKind Kind, string Target, string Arguments, string IconParsingName);

/// <summary>Lists what can be added: everything in Start's All apps plus desktop shortcuts (SPEC 4.1, spike S5).</summary>
public static unsafe class AppScanner
{
    private const string AppsFolderPrefix = @"shell:AppsFolder\";
    private static readonly string[] DesktopExtensions = [".lnk", ".url", ".appref-ms"];

    /// <summary>Scans on the calling thread (call from a background task; takes about a second).</summary>
    public static IReadOnlyList<ScannedApp> Scan(CancellationToken cancel = default)
    {
        var results = new List<ScannedApp>();
        results.AddRange(ScanAppsFolder(cancel));
        results.AddRange(ScanDesktop());
        return results
            .GroupBy(a => (a.Source, a.Kind, a.Target.ToUpperInvariant(), a.Arguments))
            .Select(g => g.First())
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal static bool IsUninstaller(string name, string target)
    {
        var file = Path.GetFileName(target);
        return file.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
            || file.StartsWith("uninstall", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Uninstall ", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ScannedApp> ScanAppsFolder(CancellationToken cancel)
    {
        var list = new List<ScannedApp>();
        IShellItem? folder = null;
        IEnumShellItems? items = null;
        try
        {
            var folderId = PInvoke.FOLDERID_AppsFolder;
            var itemIid = typeof(IShellItem).GUID;
            if (PInvoke.SHGetKnownFolderItem(&folderId, KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT, HANDLE.Null, &itemIid, out var folderObject).Failed)
            {
                return list;
            }
            folder = (IShellItem)folderObject;
            var enumIid = typeof(IEnumShellItems).GUID;
            var handler = PInvoke.BHID_EnumItems;
            folder.BindToHandler(null, &handler, &enumIid, out var enumObject);
            items = (IEnumShellItems)enumObject;

            var batch = new IShellItem[1];
            while (!cancel.IsCancellationRequested)
            {
                uint fetched = 0;
                items.Next(1, batch, &fetched);
                if (fetched == 0)
                {
                    break;
                }
                var item = (IShellItem2)batch[0];
                try
                {
                    if (ToApp(item) is { } app)
                    {
                        list.Add(app);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(item);
                }
            }
        }
        catch (COMException)
        {
            // A broken shell extension should not stop the rest of the scan.
        }
        finally
        {
            if (items is not null)
            {
                Marshal.ReleaseComObject(items);
            }
            if (folder is not null)
            {
                Marshal.ReleaseComObject(folder);
            }
        }
        return list;
    }

    private static ScannedApp? ToApp(IShellItem2 item)
    {
        var name = GetString(item, PInvoke.PKEY_ItemNameDisplay);
        var parsingName = GetDisplayName(item);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(parsingName))
        {
            return null;
        }
        var iconName = AppsFolderPrefix + parsingName;
        if (parsingName.Contains('!', StringComparison.Ordinal))
        {
            return new ScannedApp(name, ScanSource.Store, LinkKind.ShellApp, parsingName, "", iconName);
        }

        var target = GetString(item, PInvoke.PKEY_Link_TargetParsingPath);
        if (string.IsNullOrWhiteSpace(target))
        {
            // No file behind it (for example an auto-generated id); open it the way Start does.
            return new ScannedApp(name, ScanSource.Program, LinkKind.ShellApp, parsingName, "", iconName);
        }
        if (IsUninstaller(name, target))
        {
            return null;
        }
        var arguments = GetString(item, PInvoke.PKEY_Link_Arguments) ?? "";
        var kind = Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)
            ? LinkKind.App
            : LinkKindDetector.Detect(target, SystemPathProbe.Instance)?.Kind ?? LinkKind.File;
        return new ScannedApp(name, ScanSource.Program, kind, target, arguments, iconName);
    }

    private static IEnumerable<ScannedApp> ScanDesktop()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        };
        foreach (var folder in folders.Where(f => f.Length > 0 && Directory.Exists(f)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder).Where(f => DesktopExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var kind = Path.GetExtension(file).Equals(".url", StringComparison.OrdinalIgnoreCase) ? LinkKind.File : LinkKind.App;
                yield return new ScannedApp(name, ScanSource.Desktop, kind, file, "", file);
            }
        }
    }

    private static string? GetDisplayName(IShellItem2 item)
    {
        try
        {
            PWSTR value;
            item.GetDisplayName(SIGDN.SIGDN_PARENTRELATIVEPARSING, &value);
            var text = value.ToString();
            Marshal.FreeCoTaskMem((nint)value.Value);
            return text;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? GetString(IShellItem2 item, PROPERTYKEY key)
    {
        try
        {
            PWSTR value;
            item.GetString(&key, &value);
            var text = value.ToString();
            Marshal.FreeCoTaskMem((nint)value.Value);
            return text;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
