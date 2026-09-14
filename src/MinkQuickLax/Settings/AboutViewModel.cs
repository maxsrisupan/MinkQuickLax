using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Services;

namespace MinkQuickLax.Settings;

/// <param name="Name">Product name, not translated.</param>
/// <param name="License">SPDX license id, not translated.</param>
public sealed record Component(string Name, string License);

/// <summary>The About page (SPEC 4.8): version, links, licenses and a bug-report summary.</summary>
public sealed partial class AboutViewModel(IMonitorProvider monitors, ConfigStore store, StartupRegistration startup, Localizer text) : ObservableObject
{
    private const int LogLines = 200;

    public string VersionText => text.Format("About_Version", AppInfo.Version);

    public IReadOnlyList<Component> Components { get; } =
    [
        new(".NET, WPF", "MIT"),
        new("WPF-UI", "MIT"),
        new("CommunityToolkit.Mvvm", "MIT"),
        new("H.NotifyIcon", "MIT"),
        new("Microsoft.Windows.CsWin32", "MIT"),
        new("Microsoft.Extensions.Hosting", "MIT"),
        new("Serilog", "Apache-2.0"),
        new("IBM Plex Sans Thai", "OFL-1.1"),
        new("Chakra Petch", "OFL-1.1"),
        new("JetBrains Mono", "OFL-1.1"),
        new("Doto", "OFL-1.1"),
    ];

    [ObservableProperty]
    private bool _copied;

    public void Refresh()
    {
        Copied = false;
        OnPropertyChanged(nameof(VersionText));
    }

    [RelayCommand]
    private static void OpenGitHub() => Process.Start(new ProcessStartInfo { FileName = AppInfo.GitHubUrl, UseShellExecute = true })?.Dispose();

    [RelayCommand]
    private static void OpenLicenses()
    {
        if (Directory.Exists(AppInfo.LicensesDirectory))
        {
            Process.Start(new ProcessStartInfo { FileName = AppInfo.LicensesDirectory, UseShellExecute = true })?.Dispose();
        }
    }

    [RelayCommand]
    private void CopyInfo()
    {
        Clipboard.SetText(BuildReport());
        Copied = true;
    }

    /// <summary>A plain technical summary for bug reports; its field names stay in English on purpose.</summary>
    private string BuildReport()
    {
        var invariant = CultureInfo.InvariantCulture;
        var report = new StringBuilder();
        report.AppendLine(invariant, $"MinkQuickLax {AppInfo.Version} (installed: {AppInfo.IsInstalled})");
        report.AppendLine(invariant, $"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        report.AppendLine(invariant, $".NET: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine(invariant, $"UI language: {text.Culture.Name}, Windows language: {CultureInfo.InstalledUICulture.Name}");
        report.AppendLine(invariant, $"Start with Windows: {startup.Read()}");
        var config = store.Current;
        report.AppendLine(invariant, $"Links: {config.Links.Count}, groups: {config.Groups.Count}, on screen: {config.Placements.Count}, icon size: {config.Settings.IconSize}");
        foreach (var monitor in monitors.GetMonitors())
        {
            report.AppendLine(invariant, $"Screen: {monitor.Bounds} work {monitor.WorkArea} dpi {monitor.Dpi}{(monitor.IsPrimary ? " primary" : "")}");
        }
        report.AppendLine();
        report.AppendLine(invariant, $"Last {LogLines} log lines:");
        foreach (var line in ReadLogTail())
        {
            report.AppendLine(line);
        }
        return report.ToString();
    }

    private static List<string> ReadLogTail()
    {
        try
        {
            var newest = new DirectoryInfo(AppServices.LogDirectory).EnumerateFiles("log-*.txt").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            if (newest is null)
            {
                return [];
            }
            // Serilog keeps the file open, so share it for reading.
            using var stream = new FileStream(newest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var tail = new Queue<string>(LogLines);
            while (reader.ReadLine() is { } line)
            {
                if (tail.Count == LogLines)
                {
                    tail.Dequeue();
                }
                tail.Enqueue(line);
            }
            return [.. tail];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [ex.Message];
        }
    }
}
