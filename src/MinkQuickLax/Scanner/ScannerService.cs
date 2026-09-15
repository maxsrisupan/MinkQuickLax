using MinkQuickLax.Core.Config;
using MinkQuickLax.Services;

namespace MinkQuickLax.Scanner;

/// <summary>Opens the scanner (one at a time) from the tray, the edit toolbar or the first run.</summary>
public sealed class ScannerService(ConfigStore store, LinkAdder adder, ThemeService theme, Localizer text, AddWeb.AddWebService addWeb)
{
    private ScannerWindow? _window;

    public void Show(bool firstRun = false)
    {
        if (_window is { IsVisible: true })
        {
            _window.Activate();
            return;
        }
        var model = new ScannerViewModel(adder, text, store.Current.Links, firstRun);
        _window = new ScannerWindow(model, theme, addWeb);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }
}
