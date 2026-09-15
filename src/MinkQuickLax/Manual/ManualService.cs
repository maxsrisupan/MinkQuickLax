using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using MinkQuickLax.Services;

namespace MinkQuickLax.Manual;

/// <summary>
/// Opens the manual (SPEC 4.9): in the app's own window at a section, in the app's language and theme, or in the default
/// browser when the WebView2 Runtime is missing (then it cannot jump to the section).
/// </summary>
public sealed partial class ManualService(ThemeService theme, Localizer text, ILogger<ManualService> logger)
{
    private ManualWindow? _window;

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Manual");

    /// <summary>Where WebView2 keeps its profile (spike S8).</summary>
    public static string WebViewDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinkQuickLax", "WebView2");

    public static bool IsWebViewAvailable
    {
        get
        {
            try
            {
                return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch (WebView2RuntimeNotFoundException)
            {
                return false;
            }
        }
    }

    /// <summary>The manual file for the app's language.</summary>
    public string FilePath => Path.Combine(Directory, (text.Culture.TwoLetterISOLanguageName == "th" ? "th" : "en") + ".html");

    /// <param name="topic">A section id from <see cref="ManualTopics"/>, or null for the start.</param>
    public void Show(string? topic = null) => _ = ShowAsync(topic);

    private async Task ShowAsync(string? topic)
    {
        var file = FilePath;
        if (!File.Exists(file))
        {
            LogMissing(logger, file);
            return;
        }
        if (!IsWebViewAvailable)
        {
            LogNoRuntime(logger);
            OpenInBrowser(file);
            return;
        }
        var window = _window;
        if (window is null)
        {
            window = _window = new ManualWindow(theme, WebViewDataDirectory, OpenInBrowser);
            window.Closed += (_, _) => _window = null;
            window.Show();
        }
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Activate();
        try
        {
            await window.NavigateAsync(Address(file, topic));
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or InvalidOperationException or COMException or IOException or UnauthorizedAccessException)
        {
            // The runtime was removed or its profile folder is not writable: show the page in the browser instead.
            LogWebViewFailed(logger, ex);
            window.Close();
            OpenInBrowser(file);
        }
    }

    /// <summary>The file URL with the theme and the section; <c>embedded=1</c> hides the page's own print button.</summary>
    public Uri Address(string file, string? topic)
    {
        var builder = new UriBuilder(new Uri(file))
        {
            Query = $"theme={(theme.Current.SurfacesDark ? "dark" : "light")}&embedded=1",
            Fragment = topic ?? "",
        };
        return builder.Uri;
    }

    /// <summary>Opens the page in the default browser. Windows drops the section part of a file address, so it opens at the top.</summary>
    public void OpenInBrowser(string file)
    {
        try
        {
            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            LogBrowserFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Manual file not found: {Path}")]
    private static partial void LogMissing(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "WebView2 Runtime not found; opening the manual in the browser")]
    private static partial void LogNoRuntime(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The manual window could not start WebView2; opening the browser")]
    private static partial void LogWebViewFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not open the manual in the browser")]
    private static partial void LogBrowserFailed(ILogger logger, Exception exception);
}
