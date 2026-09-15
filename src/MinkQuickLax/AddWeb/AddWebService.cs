using System.Windows;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.AddWeb;

/// <summary>Opens "Add website" (one at a time) from the tray, the scanner or the settings window.</summary>
public sealed class AddWebService(ConfigStore store, LinkAdder adder, IconCache icons, ThemeService theme, Localizer text)
{
    private AddWebWindow? _window;

    public void Show()
    {
        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }
        var model = new AddWebViewModel(adder, icons, text, store.Current.Links);
        _window = new AddWebWindow(model, theme);
        // Opened from the scanner or the settings window: stay over it. From the tray no window of ours is active.
        if (Application.Current.Windows.OfType<StyledWindow>().FirstOrDefault(w => w.IsActive) is { } owner)
        {
            _window.Owner = owner;
            _window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _window.ShowInTaskbar = false;
        }
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }
}
