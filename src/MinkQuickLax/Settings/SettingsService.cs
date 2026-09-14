using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace MinkQuickLax.Settings;

/// <summary>Opens the one settings window, or brings it forward, at a page, link or group (SPEC 4.2, 4.3, 7).</summary>
public sealed class SettingsService(IServiceProvider services)
{
    private SettingsWindow? _window;

    /// <param name="page">Null keeps the page an open window is on; a new window starts at the first page.</param>
    public void Show(SettingsPage? page = null, string? item = null)
    {
        if (_window is null)
        {
            _window = ActivatorUtilities.CreateInstance<SettingsWindow>(services);
            _window.Closed += (_, _) => _window = null;
            _window.ShowPage(page ?? SettingsPage.Links, item);
            _window.Show();
        }
        else if (page is { } target)
        {
            _window.ShowPage(target, item);
        }
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }
        _window.Activate();
    }

    public void ShowLink(string linkId) => Show(SettingsPage.Links, linkId);

    public void ShowGroup(string groupId) => Show(SettingsPage.Groups, groupId);
}
