using System.Windows.Threading;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Surfaces;
using MinkQuickLax.Styles;

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

    /// <summary>Re-evaluates every icon for the current cursor position (after settings or layout changes).</summary>
    public void Refresh() => OnMoved(MouseProximityTracker.CursorPosition());

    public void Dispose()
    {
        _tracker.Moved -= OnMoved;
        _tick.Stop();
    }

    private void OnMoved(PixelPoint cursor)
    {
        foreach (var window in _windows())
        {
            if (!window.IsVisible)
            {
                continue;
            }
            var rect = window.IconRect;
            var scale = rect.Width / Math.Max(window.Width - 2 * IconDesign.WindowMargin, 1);
            var radius = RadiusDip * scale;
            // Measured from the icon's edge, so t is 1 anywhere on the icon (SPEC 5.8) and 0 beyond the radius.
            var fromEdge = Math.Max(0, rect.Center.DistanceTo(cursor) - rect.Width / 2.0);
            var t = Math.Clamp(1 - fromEdge / radius, 0, 1);
            var opacity = IdleOpacity + (1 - IdleOpacity) * t;
            var size = Magnify ? 1 + (IconDesign.MaxMagnify - 1) * t * t : 1;

            if (Math.Abs(opacity - window.TargetOpacity) < 0.01 && Math.Abs(size - window.TargetScale) < 0.005 && Math.Abs(t - window.TargetNearness) < 0.01)
            {
                continue;
            }
            window.TargetOpacity = opacity;
            window.TargetScale = size;
            window.TargetNearness = t;
            if (ReduceMotion)
            {
                window.CurrentOpacity = opacity;
                window.CurrentScale = size;
                window.CurrentNearness = t;
                window.SetProximity(opacity, size, t);
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
            var nearness = window.CurrentNearness + (window.TargetNearness - window.CurrentNearness) * Step;
            if (Math.Abs(window.TargetOpacity - opacity) < OpacityEpsilon && Math.Abs(window.TargetScale - size) < ScaleEpsilon && Math.Abs(window.TargetNearness - nearness) < OpacityEpsilon)
            {
                (opacity, size, nearness) = (window.TargetOpacity, window.TargetScale, window.TargetNearness);
                _moving.Remove(window);
            }
            window.CurrentOpacity = opacity;
            window.CurrentScale = size;
            window.CurrentNearness = nearness;
            window.SetProximity(opacity, size, nearness);
        }
        if (_moving.Count == 0)
        {
            _tick.Stop();
        }
    }
}
