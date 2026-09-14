using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using MinkQuickLax.Services;

namespace MinkQuickLax.Tray;

/// <summary>Owns the notification-area icon and its menu.</summary>
internal sealed class TrayController : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/AppIcon.ico");

    private readonly TaskbarIcon _icon;

    public TrayController(Application app)
    {
        var exitItem = new MenuItem();
        exitItem.SetBinding(HeaderedItemsControl.HeaderProperty, Localizer.Instance.Bind("Tray_Exit"));
        exitItem.Click += (_, _) => app.Shutdown();

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(IconUri),
            ContextMenu = new ContextMenu { Items = { exitItem } },
            NoLeftClickDelay = true,
        };
        _icon.SetBinding(TaskbarIcon.ToolTipTextProperty, Localizer.Instance.Bind("Tray_ToolTip"));

        // Created from code rather than XAML, so the icon must be added to the tray explicitly.
        // Efficiency mode stays off: the launcher has to react to the mouse immediately.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void Dispose() => _icon.Dispose();
}
