using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Surfaces;
using MinkQuickLax.Styles;
using PhysicalKeys = MinkQuickLax.Platform.Input.Keyboard;

namespace MinkQuickLax.Services;

/// <summary>
/// Clicks, drags and edit mode (SPEC 4.2, 4.4): in use mode a click opens the link and Ctrl+drag moves the icon;
/// in edit mode icons wiggle, drag with grid/alignment snapping and guides, avoid overlapping on drop, and changes
/// can be undone, redone or cancelled all at once.
/// </summary>
public sealed partial class ArrangeController : IDisposable
{
    private const double DragThresholdDip = 4;

    private readonly ConfigStore _store;
    private readonly PlacementController _placements;
    private readonly SurfaceHost _surfaces;
    private readonly ThemeService _theme;
    private readonly Localizer _text;
    private readonly ILogger<ArrangeController> _logger;
    private readonly UndoHistory<AppConfig> _undo = new();
    private readonly GuideWindow _verticalGuide = new(vertical: true);
    private readonly GuideWindow _horizontalGuide = new(vertical: false);
    private readonly DragReadoutWindow _readout = new();
    private AppConfig? _entrySnapshot;
    private EditToolbarWindow? _toolbar;
    private string? _selectedId;

    // Current press / drag.
    private IconWindow? _pressed;
    private PixelPoint _pressPoint;
    private PixelPoint _grabOffset;
    private bool _doubleClick;
    private bool _dragging;
    private AppConfig? _beforeDrag;
    private MonitorInfo? _dragMonitor;
    private PixelPoint _dragCenter;

    // Dropping on another item: a folder takes the link in; resting on a single icon for 0.6 s makes a new group.
    private readonly System.Windows.Threading.DispatcherTimer _holdTimer = new() { Interval = IconDesign.GroupHold };
    private IconWindow? _dropTarget;
    private bool _dropReady;

    public ArrangeController(ConfigStore store, PlacementController placements, SurfaceHost surfaces, ThemeService theme, Localizer text, ILogger<ArrangeController> logger)
    {
        _store = store;
        _placements = placements;
        _surfaces = surfaces;
        _theme = theme;
        _text = text;
        _logger = logger;
        _placements.WindowCreated += Hook;
        _placements.WindowRemoved += OnWindowRemoved;
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            if (_dragging && _dropTarget is { } target)
            {
                _dropReady = true;
                target.SetDropTarget(true);
            }
        };
        _placements.HiddenChanged += hidden =>
        {
            if (hidden && IsArranging)
            {
                Done();
            }
        };
    }

    public bool IsArranging { get; private set; }

    /// <summary>"Add" on the toolbar; wired up when adding links exists (M5).</summary>
    public Action? AddRequested { get; set; }

    public void Enter()
    {
        if (IsArranging)
        {
            _toolbar?.Activate();
            return;
        }
        _placements.SetHidden(false);
        _surfaces.ClosePopup();
        IsArranging = true;
        _entrySnapshot = _store.Current;
        _undo.Clear();
        _placements.SetArranging(true);

        var monitors = _placements.Monitors;
        var primary = PositionMapper.Primary(monitors);
        _toolbar = new EditToolbarWindow(_theme, _text, () => AddRequested?.Invoke(), AddRequested is not null, Tidy, Cancel, Done);
        _toolbar.KeyPressed += OnKey;
        _toolbar.ShowOn(primary.WorkArea, primary.Scale);
        LogEntered(_logger);
    }

    /// <summary>Keeps the changes and leaves edit mode.</summary>
    public void Done() => Exit(restore: false);

    /// <summary>Restores everything as it was before edit mode and leaves it.</summary>
    public void Cancel() => Exit(restore: true);

    public void Tidy()
    {
        Record();
        _placements.Tidy();
    }

    public void Dispose()
    {
        _placements.WindowCreated -= Hook;
        _placements.WindowRemoved -= OnWindowRemoved;
        _toolbar?.Close();
        _verticalGuide.Close();
        _horizontalGuide.Close();
        _readout.Close();
    }

    private void Exit(bool restore)
    {
        if (!IsArranging)
        {
            return;
        }
        // Enter pressed mid-drag: settle the icon first so no guide or readout is left behind.
        FinishPress();
        if (restore && _entrySnapshot is { } snapshot)
        {
            _store.Update(_ => snapshot);
        }
        IsArranging = false;
        _entrySnapshot = null;
        _undo.Clear();
        _selectedId = null;
        _placements.SetArranging(false);
        var toolbar = _toolbar;
        _toolbar = null;
        toolbar?.Close();
        LogExited(_logger, restore);
    }

    private void Hook(IconWindow window)
    {
        window.Pressed += OnPressed;
        window.PointerMoved += OnPointerMoved;
        window.Released += OnReleased;
        window.CaptureLost += OnCaptureLost;
    }

    private void OnWindowRemoved(IconWindow window)
    {
        if (ReferenceEquals(_dropTarget, window))
        {
            ClearDropTarget();
        }
        if (ReferenceEquals(_pressed, window))
        {
            EndDragVisuals(window);
            _pressed = null;
            _dragging = false;
        }
        if (_selectedId == window.PlacementId)
        {
            _selectedId = null;
        }
    }

    private void OnPressed(IconWindow window, int clickCount)
    {
        // A press can arrive before the last one was released (pen, touch, injected input).
        FinishPress();
        _pressed = window;
        _pressPoint = MouseProximityTracker.CursorPosition();
        _grabOffset = new PixelPoint(_pressPoint.X - window.SquareRect.Center.X, _pressPoint.Y - window.SquareRect.Center.Y);
        _doubleClick = clickCount >= 2;
        _dragging = false;
        window.BeginCapture();
        if (IsArranging)
        {
            Select(window.PlacementId);
        }
    }

    private void OnPointerMoved(IconWindow window)
    {
        if (!ReferenceEquals(_pressed, window))
        {
            return;
        }
        var cursor = MouseProximityTracker.CursorPosition();
        if (!_dragging)
        {
            var scale = PositionMapper.MonitorAt(cursor, _placements.Monitors).Scale;
            var mayDrag = IsArranging || (_store.Current.Settings.CtrlDragMove && PhysicalKeys.IsCtrlDown);
            if (!mayDrag || cursor.DistanceTo(_pressPoint) < DragThresholdDip * scale)
            {
                return;
            }
            StartDrag(window);
        }
        UpdateDrag(window, cursor);
    }

    private void OnReleased(IconWindow window)
    {
        if (!ReferenceEquals(_pressed, window))
        {
            return;
        }
        _pressed = null;
        window.EndCapture();
        if (_dragging)
        {
            Drop(window);
            _toolbar?.TakeKeyboardFocus();
            return;
        }
        if (IsArranging)
        {
            // Folders still open in edit mode, so links can be reordered or dragged out of the panel.
            if (window.IsFolder && window.SquareRect.Contains(MouseProximityTracker.CursorPosition()))
            {
                _placements.RequestLaunch(window);
            }
            _toolbar?.TakeKeyboardFocus();
            return;
        }
        // Like a button: moving away before letting go cancels the click.
        var cursor = MouseProximityTracker.CursorPosition();
        var scale = PositionMapper.MonitorAt(cursor, _placements.Monitors).Scale;
        if (!window.SquareRect.Contains(cursor) || cursor.DistanceTo(_pressPoint) >= DragThresholdDip * scale)
        {
            return;
        }
        var onDoubleClick = _store.Current.Settings.LaunchOn == LaunchTrigger.DoubleClick;
        if (!onDoubleClick || _doubleClick)
        {
            _placements.RequestLaunch(window);
        }
    }

    /// <summary>Ends a press that is still open; a drag in progress is dropped where it is.</summary>
    private void FinishPress()
    {
        if (_pressed is not { } window)
        {
            return;
        }
        _pressed = null;
        window.EndCapture();
        if (_dragging)
        {
            Drop(window);
        }
    }

    private void OnCaptureLost(IconWindow window)
    {
        // Losing capture mid-drag (another app grabbed the mouse): keep the icon where it is now.
        if (ReferenceEquals(_pressed, window) && _dragging)
        {
            _pressed = null;
            Drop(window);
        }
    }

    private void StartDrag(IconWindow window)
    {
        _dragging = true;
        _beforeDrag = _store.Current;
        _surfaces.HideTooltip();
        _surfaces.ClosePopup();
        window.SetLifted(true);
        LogDragStarted(_logger, window.PlacementId, IsArranging);
    }

    private void UpdateDrag(IconWindow window, PixelPoint cursor)
    {
        var settings = _store.Current.Settings;
        var monitors = _placements.Monitors;
        var monitor = PositionMapper.MonitorAt(cursor, monitors);
        var area = PositionMapper.PlacementArea(monitor, settings.AllowOverTaskbar);
        var size = PositionMapper.WindowSize(settings.IconSize, monitor);
        var desired = new PixelPoint(cursor.X - _grabOffset.X, cursor.Y - _grabOffset.Y);
        var others = OtherRects(window, monitor);
        var snap = SnapEngine.Snap(desired, size, others, area, settings.Snap, monitor.ToPixels(settings.GridSize), monitor.ToPixels(Motion.AlignThreshold));

        _dragMonitor = monitor;
        _dragCenter = snap.Center;
        window.MoveTo(PixelRect.FromCenter(snap.Center, size), monitor.Dpi);
        TrackDropTarget(window, cursor);

        var style = _theme.Current.Style;
        ShowGuide(_verticalGuide, snap.Guides.FirstOrDefault(g => g.Vertical), area, monitor.Scale, style);
        ShowGuide(_horizontalGuide, snap.Guides.FirstOrDefault(g => !g.Vertical), area, monitor.Scale, style);
        var text = string.Format(CultureInfo.InvariantCulture, _text["Readout_Position"], snap.Center.X - area.Left, snap.Center.Y - area.Top);
        _readout.ShowBeside(text, window.IconRect, area, monitor.Scale, style);
    }

    private void TrackDropTarget(IconWindow dragged, PixelPoint cursor)
    {
        // Only single icons join or form groups; a dragged folder just moves.
        var target = dragged.IsFolder
            ? null
            : _placements.Windows.FirstOrDefault(w => !ReferenceEquals(w, dragged) && w.IsVisible && w.SquareRect.Contains(cursor));
        if (ReferenceEquals(target, _dropTarget))
        {
            return;
        }
        ClearDropTarget();
        _dropTarget = target;
        if (target is null)
        {
            return;
        }
        if (target.IsFolder)
        {
            _dropReady = true;
            target.SetDropTarget(true);
        }
        else
        {
            _holdTimer.Start();
        }
    }

    private void ClearDropTarget()
    {
        _holdTimer.Stop();
        _dropTarget?.SetDropTarget(false);
        _dropTarget = null;
        _dropReady = false;
    }

    /// <summary>Returns true when the drop went into a group (or made one) instead of moving the icon.</summary>
    private bool DropOnTarget(IconWindow dragged)
    {
        var target = _dropTarget;
        var ready = _dropReady;
        ClearDropTarget();
        var config = _store.Current;
        if (target is null || !ready || config.FindPlacement(dragged.PlacementId) is not { Type: PlacementType.Link } draggedPlacement
            || config.FindPlacement(target.PlacementId) is not { } targetPlacement)
        {
            return false;
        }

        Record();
        if (targetPlacement.Type == PlacementType.Group)
        {
            // SPEC 4.4: into the group; the single icon goes away.
            _store.Update(c => c.AddLinkToGroup(targetPlacement.RefId, draggedPlacement.RefId).RemovePlacement(draggedPlacement.Id));
            LogJoinedGroup(_logger, draggedPlacement.RefId, targetPlacement.RefId);
        }
        else
        {
            // SPEC 4.4: rested on another icon: both become a new group where the target was.
            var group = new Group { Name = _text["Group_DefaultName"], LinkIds = [targetPlacement.RefId, draggedPlacement.RefId] };
            var groupPlacement = targetPlacement with { Id = Ids.New(), Type = PlacementType.Group, RefId = group.Id };
            _store.Update(c => c.RemovePlacement(targetPlacement.Id).RemovePlacement(draggedPlacement.Id).CreateGroup(group, groupPlacement));
            LogGroupCreated(_logger, group.Id);
        }
        _selectedId = null;
        return true;
    }

    private void Drop(IconWindow window)
    {
        _dragging = false;
        EndDragVisuals(window);
        // The config does not change while dragging, so recording now still captures the state before the drag.
        if (DropOnTarget(window))
        {
            _beforeDrag = null;
            _dragMonitor = null;
            return;
        }
        if (_dragMonitor is not { } monitor || _store.Current.FindPlacement(window.PlacementId) is not { } placement)
        {
            return;
        }
        var settings = _store.Current.Settings;
        var area = PositionMapper.PlacementArea(monitor, settings.AllowOverTaskbar);
        var size = PositionMapper.WindowSize(settings.IconSize, monitor);
        var center = CollisionResolver.FindFreeCenter(_dragCenter, size, OtherRects(window, monitor), area, monitor.ToPixels(settings.GridSize));
        window.MoveTo(PixelRect.FromCenter(center, size), monitor.Dpi);

        if (IsArranging && _beforeDrag is { } before)
        {
            _undo.Push(before);
        }
        _store.Update(c => c.UpdatePlacement(PositionMapper.WithCenter(placement, center, monitor, settings.AllowOverTaskbar)));
        _beforeDrag = null;
        _dragMonitor = null;
        LogDropped(_logger, window.PlacementId, center.X, center.Y);
    }

    private void EndDragVisuals(IconWindow window)
    {
        window.SetLifted(false);
        _verticalGuide.Hide();
        _horizontalGuide.Hide();
        _readout.Hide();
        if (IsArranging)
        {
            window.SetArranging(true, 0, _theme.Current.ReduceMotion);
        }
    }

    private List<PixelRect> OtherRects(IconWindow dragged, MonitorInfo monitor) =>
        _placements.Windows
            .Where(w => !ReferenceEquals(w, dragged) && w.IsVisible && w.SquareRect.IntersectsWith(monitor.Bounds))
            .Select(w => w.SquareRect)
            .ToList();

    /// <summary>The guide runs across the whole placement area, like the mockup, not just between the two items.</summary>
    private static void ShowGuide(GuideWindow window, GuideLine? line, PixelRect area, double scale, Core.Model.StyleSetting style)
    {
        if (line is null)
        {
            window.Hide();
        }
        else
        {
            window.ShowLine(line.Vertical ? line with { Start = area.Top, End = area.Bottom } : line with { Start = area.Left, End = area.Right }, scale, style);
        }
    }

    private void Select(string? placementId)
    {
        _selectedId = placementId;
        foreach (var window in _placements.Windows)
        {
            window.SetSelected(window.PlacementId == placementId);
        }
    }

    private void OnKey(Key key, ModifierKeys modifiers)
    {
        var ctrl = modifiers.HasFlag(ModifierKeys.Control);
        var shift = modifiers.HasFlag(ModifierKeys.Shift);
        LogKey(_logger, key, modifiers, _selectedId);
        switch (key)
        {
            case Key.Left: Nudge(-1, 0, shift); break;
            case Key.Right: Nudge(1, 0, shift); break;
            case Key.Up: Nudge(0, -1, shift); break;
            case Key.Down: Nudge(0, 1, shift); break;
            case Key.Delete: RemoveSelected(); break;
            case Key.Z when ctrl && shift: Redo(); break;
            case Key.Z when ctrl: Undo(); break;
            case Key.Y when ctrl: Redo(); break;
            case Key.Enter: Done(); break;
        }
    }

    /// <summary>Arrow keys: one grid step, or one pixel with Shift (SPEC 4.4).</summary>
    private void Nudge(int dx, int dy, bool fine)
    {
        var config = _store.Current;
        if (_selectedId is null || config.FindPlacement(_selectedId) is not { } placement)
        {
            return;
        }
        var settings = config.Settings;
        var position = PositionMapper.ToScreen(placement, settings.IconSize, _placements.Monitors, settings.AllowOverTaskbar);
        var monitor = position.Monitor;
        var step = fine ? 1 : monitor.ToPixels(settings.GridSize);
        var area = PositionMapper.PlacementArea(monitor, settings.AllowOverTaskbar);
        var moved = position.Rect.Offset(dx * step, dy * step).MoveInside(area);
        if (moved == position.Rect)
        {
            return;
        }
        Record();
        _store.Update(c => c.UpdatePlacement(PositionMapper.WithCenter(placement, moved.Center, monitor, settings.AllowOverTaskbar)));
        _placements.WindowFor(placement.Id)?.SetSelected(true);
    }

    private void RemoveSelected()
    {
        if (_selectedId is not { } id || _store.Current.FindPlacement(id) is null)
        {
            return;
        }
        Record();
        _selectedId = null;
        _store.Update(c => c.RemovePlacement(id));
    }

    private void Undo()
    {
        if (_undo.TryUndo(_store.Current, out var previous))
        {
            _store.Update(_ => previous);
            Select(_selectedId);
        }
    }

    private void Redo()
    {
        if (_undo.TryRedo(_store.Current, out var next))
        {
            _store.Update(_ => next);
            Select(_selectedId);
        }
    }

    private void Record()
    {
        if (IsArranging)
        {
            _undo.Push(_store.Current);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Edit mode on")]
    private static partial void LogEntered(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Edit mode off (cancelled: {Cancelled})")]
    private static partial void LogExited(ILogger logger, bool cancelled);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Drag started {Id} (edit mode: {Arranging})")]
    private static partial void LogDragStarted(ILogger logger, string id, bool arranging);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Edit mode key {Key} {Modifiers} (selected: {Selected})")]
    private static partial void LogKey(ILogger logger, Key key, ModifierKeys modifiers, string? selected);

    [LoggerMessage(Level = LogLevel.Information, Message = "Link {LinkId} joined group {GroupId}")]
    private static partial void LogJoinedGroup(ILogger logger, string linkId, string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Group {Id} created by dropping one icon on another")]
    private static partial void LogGroupCreated(ILogger logger, string id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dropped {Id} at {X},{Y}")]
    private static partial void LogDropped(ILogger logger, string id, int x, int y);
}
