using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Surfaces;

namespace MinkQuickLax.Services;

/// <summary>
/// Opens a group's panel from its folder and handles the links inside it (SPEC 4.3, 4.4): click to open (the panel
/// closes), drag within the panel to reorder, drag out of the panel to make a single icon.
/// </summary>
public sealed partial class GroupController
{
    private const double DragThresholdDip = 4;

    private readonly ConfigStore _store;
    private readonly PlacementController _placements;
    private readonly SurfaceHost _surfaces;
    private readonly IconCache _icons;
    private readonly ThemeService _theme;
    private readonly Localizer _text;
    private readonly ILogger<GroupController> _logger;

    private GroupPanelWindow? _panel;
    private IconWindow? _folder;
    private PanelItem? _pressed;
    private PixelPoint _pressPoint;
    private IconWindow? _ghost;

    public GroupController(ConfigStore store, PlacementController placements, SurfaceHost surfaces, IconCache icons, ThemeService theme, Localizer text, ILogger<GroupController> logger)
    {
        _store = store;
        _placements = placements;
        _surfaces = surfaces;
        _icons = icons;
        _theme = theme;
        _text = text;
        _logger = logger;
        _placements.GroupOpenRequested += Toggle;
        _store.Changed += OnConfigChanged;
    }

    /// <summary>A link inside a panel was clicked; the folder window anchors notices.</summary>
    public event Action<IconWindow, Link>? LinkLaunchRequested;

    /// <summary>A link inside a panel was right-clicked.</summary>
    public event Action<IconWindow, Link, Group>? LinkMenuRequested;

    public void Toggle(IconWindow folder, Group group)
    {
        if (_panel is { IsVisible: true } open && open.Group.Id == group.Id)
        {
            Close();
            return;
        }
        Open(folder, group);
    }

    public void Close()
    {
        _surfaces.ClosePopup();
        _panel = null;
    }

    private void Open(IconWindow folder, Group group)
    {
        var config = _store.Current;
        var links = group.LinkIds.Select(config.FindLink).OfType<Link>().ToList();
        var panel = new GroupPanelWindow(_theme, _text, group, links, config.Settings.IconSize, group.Columns);
        foreach (var item in panel.Items)
        {
            Hook(item);
            _ = LoadIconAsync(item);
        }
        _panel = panel;
        _folder = folder;
        panel.Closed += (_, _) =>
        {
            if (ReferenceEquals(_panel, panel))
            {
                _panel = null;
                // Closed mid-drag (Esc): the drop can no longer happen, so drop the ghost too.
                _pressed = null;
                _ghost?.Close();
                _ghost = null;
            }
        };
        _surfaces.ShowPanel(panel, folder.SquareRect);
        LogOpened(_logger, group.Id, links.Count);
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (_panel is not { IsVisible: true } panel || _folder is null || _ghost is not null)
        {
            return;
        }
        var group = e.Current.FindGroup(panel.Group.Id);
        if (group is null || e.Current.Placements.All(p => p.RefId != group.Id))
        {
            Close();
        }
        else if (!Equals(group, panel.Group) || group.LinkIds.Any(id => !Equals(e.Previous.FindLink(id), e.Current.FindLink(id))))
        {
            // Rebuild in place so the panel shows the new order or names.
            Open(_folder, group);
        }
    }

    private void Hook(PanelItem item)
    {
        item.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            if (_ghost is not null)
            {
                // A second press mid-drag (pen, touch, injected input) keeps the drag that is already running.
                _pressed?.CaptureMouse();
                return;
            }
            _pressed = item;
            _pressPoint = MouseProximityTracker.CursorPosition();
            item.CaptureMouse();
        };
        item.MouseMove += (_, _) =>
        {
            if (!ReferenceEquals(_pressed, item))
            {
                return;
            }
            var cursor = MouseProximityTracker.CursorPosition();
            if (_ghost is null && cursor.DistanceTo(_pressPoint) >= DragThresholdDip * Scale(cursor))
            {
                StartDrag(item);
            }
            if (_ghost is not null)
            {
                MoveGhost(cursor);
            }
        };
        item.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (!ReferenceEquals(_pressed, item))
            {
                return;
            }
            _pressed = null;
            item.ReleaseMouseCapture();
            if (_ghost is not null)
            {
                Drop(item);
            }
            else if (GroupPanelWindow.IsInside(item, e) && _folder is { } folder)
            {
                Close();
                LinkLaunchRequested?.Invoke(folder, item.Link);
            }
        };
        item.LostMouseCapture += (_, _) =>
        {
            // Another app took the mouse mid-drag: finish the drag where the cursor is.
            if (ReferenceEquals(_pressed, item))
            {
                _pressed = null;
                if (_ghost is not null)
                {
                    Drop(item);
                }
            }
        };
        item.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (_folder is { } folder && _panel is { } panel)
            {
                LinkMenuRequested?.Invoke(folder, item.Link, panel.Group);
            }
        };
    }

    private void StartDrag(PanelItem item)
    {
        var settings = _store.Current.Settings;
        _ghost = new IconWindow("group-drag");
        new WindowInteropHelper(_ghost).EnsureHandle();
        _ghost.SetAppearance(settings.IconSize, false, item.Link.Name, _theme.Current.Dark);
        _ghost.SetImage(item.Icon);
        _ghost.SetProximity(1, 1);
        _ghost.SetLifted(true);
        item.Opacity = 0.35;
        MoveGhost(MouseProximityTracker.CursorPosition());
        _ghost.Show();
    }

    private void MoveGhost(PixelPoint cursor)
    {
        var monitor = PositionMapper.MonitorAt(cursor, _placements.Monitors);
        var size = PositionMapper.WindowSize(_store.Current.Settings.IconSize, monitor);
        _ghost!.MoveTo(PixelRect.FromCenter(cursor, size), monitor.Dpi);
    }

    private void Drop(PanelItem item)
    {
        var cursor = MouseProximityTracker.CursorPosition();
        var ghost = _ghost!;
        _ghost = null;
        ghost.Close();
        item.Opacity = 1;
        if (_panel is not { } panel)
        {
            return;
        }

        var group = panel.Group;
        if (panel.Bounds.Contains(cursor))
        {
            var index = panel.IndexAt(cursor);
            _store.Update(c => c.MoveLinkInGroup(group.Id, item.Link.Id, index));
            LogReordered(_logger, group.Id, index);
            return;
        }

        // Out of the panel: it leaves the group and becomes a single icon where it was dropped.
        var settings = _store.Current.Settings;
        var monitor = PositionMapper.MonitorAt(cursor, _placements.Monitors);
        var area = PositionMapper.PlacementArea(monitor, settings.AllowOverTaskbar);
        var size = PositionMapper.WindowSize(settings.IconSize, monitor);
        var others = _placements.Windows.Where(w => w.IsVisible).Select(w => w.SquareRect).ToList();
        var snapped = SnapEngine.Snap(cursor, size, others, area, settings.Snap, monitor.ToPixels(settings.GridSize), monitor.ToPixels(Styles.Motion.AlignThreshold)).Center;
        var center = CollisionResolver.FindFreeCenter(snapped, size, others, area, monitor.ToPixels(settings.GridSize));
        var placement = PositionMapper.WithCenter(new Placement { Type = PlacementType.Link, RefId = item.Link.Id }, center, monitor, settings.AllowOverTaskbar);
        _store.Update(c => c.RemoveLinkFromGroup(group.Id, item.Link.Id).AddPlacement(placement));
        LogDraggedOut(_logger, item.Link.Id, group.Id);
    }

    private async Task LoadIconAsync(PanelItem item)
    {
        if (await _icons.GetAsync(item.Link) is { } image)
        {
            item.Icon = image;
        }
    }

    private double Scale(PixelPoint at) => PositionMapper.MonitorAt(at, _placements.Monitors).Scale;

    [LoggerMessage(Level = LogLevel.Information, Message = "Group {Id} opened with {Count} links")]
    private static partial void LogOpened(ILogger logger, string id, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Group {Id}: link moved to position {Index}")]
    private static partial void LogReordered(ILogger logger, string id, int index);

    [LoggerMessage(Level = LogLevel.Information, Message = "Link {LinkId} dragged out of group {GroupId}")]
    private static partial void LogDraggedOut(ILogger logger, string linkId, string groupId);
}
