using System.IO;
using System.Reflection;

namespace MinkQuickLax.Services;

/// <summary>Facts about this copy of the app: its version, where it runs from and whether it was installed.</summary>
public static class AppInfo
{
    public const string GitHubUrl = "https://github.com/maxsrisupan/MinkQuickLax";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MinkQuickLax.exe");

    public static string LicensesDirectory => Path.Combine(AppContext.BaseDirectory, "Licenses");

    public static string IconCacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinkQuickLax", "cache");

    /// <summary>
    /// True for a copy installed by the installer (Velopack puts the app in a "current" folder next to Update.exe).
    /// Development builds are not, so they never register themselves to start with Windows on their own.
    /// </summary>
    public static bool IsInstalled
    {
        get
        {
            var directory = Path.GetDirectoryName(ExePath);
            var root = directory is null ? null : Path.GetDirectoryName(directory);
            return root is not null
                && string.Equals(Path.GetFileName(directory), "current", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(root, "Update.exe"));
        }
    }
}
