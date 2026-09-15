using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Services;
using MinkQuickLax.Surfaces;

namespace MinkQuickLax.Tray;

/// <summary>The notification-area icon: left click hides/shows, right click opens the glass menu (SPEC 4.5, 4.7).</summary>
public sealed class TrayController : IDisposable
{
    private static readonly Uri IconUri = new("pack://application:,,,/Assets/AppIcon.ico");

    private readonly TaskbarIcon _icon;
    private readonly ConfigStore _store;
    private readonly PlacementController _placements;
    private readonly SurfaceHost _surfaces;
    private readonly Localizer _text;

    private readonly ArrangeController _arrange;

    private readonly Scanner.ScannerService _scanner;
    private readonly AddWeb.AddWebService _addWeb;
    private readonly Settings.SettingsService _settings;
    private readonly Manual.ManualService _manual;
    private readonly UpdateController _updates;

    public TrayController(ConfigStore store, PlacementController placements, ArrangeController arrange, Scanner.ScannerService scanner, AddWeb.AddWebService addWeb, Settings.SettingsService settings, Manual.ManualService manual, UpdateController updates, SurfaceHost surfaces, Localizer text)
    {
        _manual = manual;
        _updates = updates;
        _settings = settings;
        _scanner = scanner;
        _addWeb = addWeb;
        _store = store;
        _placements = placements;
        _arrange = arrange;
        _surfaces = surfaces;
        _text = text;

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(IconUri),
            NoLeftClickDelay = true,
            MenuActivation = PopupActivationMode.None,
        };
        _icon.TrayLeftMouseUp += (_, _) =>
        {
            if (_store.Current.Settings.TrayClickToggles)
            {
                _placements.ToggleHidden();
            }
            else
            {
                ShowMenu();
            }
        };
        _icon.TrayRightMouseUp += (_, _) => ShowMenu();
        _placements.HiddenChanged += _ => UpdateToolTip();
        _text.PropertyChanged += (_, _) => UpdateToolTip();
        UpdateToolTip();

        // Built in code, so it has to be added to the tray explicitly. Efficiency mode would slow our reactions.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    /// <summary>Raised by the Exit command.</summary>
    public event Action? ExitRequested;

    public void ShowBalloon(string message) =>
        _icon.ShowNotification(_text["Tray_ToolTip"], message, NotificationIcon.Warning);

    public void Dispose() => _icon.Dispose();

    private void UpdateToolTip() =>
        _icon.ToolTipText = _placements.IsHidden ? _text["Tray_ToolTipHidden"] : _text["Tray_ToolTip"];

    private void ShowMenu()
    {
        IReadOnlyList<MenuEntry> entries =
        [
            new MenuCommand(_text["Tray_AddFromPc"], () => _scanner.Show()),
            new MenuCommand(_text["Tray_AddWeb"], _addWeb.Show),
            new MenuCommand(_text["Tray_Arrange"], _arrange.Enter, IsEnabled: !_arrange.IsArranging),
            new MenuCommand(_text["Tray_Tidy"], _arrange.IsArranging ? _arrange.Tidy : _placements.Tidy),
            MenuSeparator.Instance,
            new MenuCommand(_placements.IsHidden ? _text["Tray_Show"] : _text["Tray_Hide"], _placements.ToggleHidden),
            MenuSeparator.Instance,
            new MenuCommand(_text["Tray_Settings"], () => _settings.Show()),
            new MenuCommand(_text["Tray_Manual"], () => _manual.Show(Manual.ManualTopics.GettingStarted)),
            _updates.ReadyVersion is { } ready
                ? new MenuCommand(_text.Format("Tray_RestartToUpdate", ready), _updates.RestartNow)
                : new MenuCommand(_text["Tray_CheckUpdates"], _updates.CheckNow),
            MenuSeparator.Instance,
            new MenuCommand(_text["Tray_Exit"], () => ExitRequested?.Invoke()),
        ];
        _surfaces.ShowMenu(MouseProximityTracker.CursorPosition(), null, entries);
    }
}
