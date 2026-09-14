using System.Windows.Threading;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Surfaces;
using MinkQuickLax.Styles.Glass;

namespace MinkQuickLax.Services;

/// <summary>
/// Makes icons clearer and larger as the mouse gets close (SPEC 4.2, 5.4). Eases toward the target itself on a
/// 33 ms tick that only runs while an icon is changing; WPF storyboards cost more CPU here (spike S3).
/// </summary>
public sealed class ProximityAnimator : IDisposable
{
    // Fraction of the remaining distance covered per tick; about 95% after 250 ms.
    private const double Step = 0.33;
    private const double OpacityEpsilon = 0.004;
    private const double ScaleEpsilon = 0.002;

    private readonly MouseProximityTracker _tracker;
    private readonly Func<IReadOnlyCollection<IconWindow>> _windows;
    private readonly DispatcherTimer _tick;
    private readonly HashSet<IconWindow> _moving = [];
    private PixelPoint _lastCursor;

    public ProximityAnimator(MouseProximityTracker tracker, Func<IReadOnlyCollection<IconWindow>> windows)
    {
        _tracker = tracker;
        _windows = windows;
        _tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = MouseProximityTracker.Interval };
        _tick.Tick += (_, _) => Advance();
        _tracker.Moved += OnMoved;
    }

    public double IdleOpacity { get; set; } = 0.7;

    public int RadiusDip { get; set; } = 130;

    public bool Magnify { get; set; } = true;

    public bool ReduceMotion { get; set; }

    /// <summary>Re-evaluates every icon for the last known cursor position (after settings or layout changes).</summary>
    public void Refresh() => OnMoved(_lastCursor);

    public void Dispose()
    {
        _tracker.Moved -= OnMoved;
        _tick.Stop();
    }

    private void OnMoved(PixelPoint cursor)
    {
        _lastCursor = cursor;
        foreach (var window in _windows())
        {
            if (!window.IsVisible)
            {
                continue;
            }
            var rect = window.IconRect;
            var scale = rect.Width / Math.Max(window.Width - 2 * IconDesign.WindowMargin, 1);
            var radius = RadiusDip * scale;
            var t = Math.Clamp(1 - rect.Center.DistanceTo(cursor) / radius, 0, 1);
            var opacity = IdleOpacity + (1 - IdleOpacity) * t;
            var size = Magnify ? 1 + (IconDesign.MaxMagnify - 1) * t * t : 1;

            if (Math.Abs(opacity - window.TargetOpacity) < 0.01 && Math.Abs(size - window.TargetScale) < 0.005)
            {
                continue;
            }
            window.TargetOpacity = opacity;
            window.TargetScale = size;
            if (ReduceMotion)
            {
                window.CurrentOpacity = opacity;
                window.CurrentScale = size;
                window.SetProximity(opacity, size);
                continue;
            }
            _moving.Add(window);
        }
        if (_moving.Count > 0 && !_tick.IsEnabled)
        {
            _tick.Start();
        }
    }

    private void Advance()
    {
        foreach (var window in _moving.ToArray())
        {
            var opacity = window.CurrentOpacity + (window.TargetOpacity - window.CurrentOpacity) * Step;
            var size = window.CurrentScale + (window.TargetScale - window.CurrentScale) * Step;
            if (Math.Abs(window.TargetOpacity - opacity) < OpacityEpsilon && Math.Abs(window.TargetScale - size) < ScaleEpsilon)
            {
                (opacity, size) = (window.TargetOpacity, window.TargetScale);
                _moving.Remove(window);
            }
            window.CurrentOpacity = opacity;
            window.CurrentScale = size;
            window.SetProximity(opacity, size);
        }
        if (_moving.Count == 0)
        {
            _tick.Stop();
        }
    }
}
