using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Manual;

/// <summary>
/// The manual in a WebView2 inside a styled window (SPEC 4.9). Only the manual's own files are shown here; web links open
/// in the default browser.
/// </summary>
public partial class ManualWindow : StyledWindow
{
    private readonly string _dataDirectory;
    private readonly Action<string> _openInBrowser;
    private Task? _ready;
    private Uri? _address;

    public ManualWindow(ThemeService theme, string dataDirectory, Action<string> openInBrowser)
    {
        _dataDirectory = dataDirectory;
        _openInBrowser = openInBrowser;
        CaptionHeight = 52;
        UseTheme(theme);
        InitializeComponent();
        // Paint the page area in the theme's color so there is no white flash before the manual loads.
        Web.DefaultBackgroundColor = theme.Current.SurfacesDark ? System.Drawing.Color.FromArgb(10, 19, 23) : System.Drawing.Color.FromArgb(232, 241, 242);
        theme.Changed += OnLookChanged;
        Closed += (_, _) => theme.Changed -= OnLookChanged;
    }

    /// <summary>Shows the page; throws when WebView2 cannot start (runtime removed, profile folder not writable).</summary>
    public async Task NavigateAsync(Uri address)
    {
        _address = address;
        _ready ??= InitializeAsync();
        await _ready;
        Web.CoreWebView2.Navigate(address.AbsoluteUri);
    }

    private async Task InitializeAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, _dataDirectory);
        await Web.EnsureCoreWebView2Async(environment);
        var settings = Web.CoreWebView2.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsWebMessageEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        Web.CoreWebView2.NavigationStarting += OnNavigationStarting;
        Web.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        PrintButton.IsEnabled = true;
    }

    /// <summary>Stays inside the manual folder; anything else (web links) goes to the default browser.</summary>
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && IsManualFile(uri))
        {
            return;
        }
        e.Cancel = true;
        OpenExternal(e.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && IsManualFile(uri))
        {
            Web.CoreWebView2.Navigate(uri.AbsoluteUri);
            return;
        }
        OpenExternal(e.Uri);
    }

    private static bool IsManualFile(Uri uri)
    {
        if (!uri.IsFile)
        {
            return false;
        }
        var folder = Path.GetFullPath(ManualService.Directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(uri.LocalPath).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    private static void OpenExternal(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        }
    }

    private void OnOpenInBrowser(object sender, RoutedEventArgs e)
    {
        // Open the language shown now, which may differ from the app's after using the page's language button.
        var current = Web.CoreWebView2?.Source is { } source && Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile ? uri : _address;
        if (current is not null)
        {
            _openInBrowser(current.LocalPath);
        }
    }

    private void OnPrint(object sender, RoutedEventArgs e) => Web.CoreWebView2?.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        Web.Dispose();
        base.OnClosed(e);
    }

    /// <summary>Keeps the page's theme in step when the app's theme changes while the manual is open.</summary>
    private void OnLookChanged(Look look)
    {
        if (Web.CoreWebView2 is { } web)
        {
            _ = web.ExecuteScriptAsync($"document.documentElement.dataset.theme = '{(look.SurfacesDark ? "dark" : "light")}'");
        }
    }
}
