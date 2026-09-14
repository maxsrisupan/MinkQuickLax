using System.Diagnostics;
using System.Windows.Media;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Styles.Glass;
using MinkQuickLax.Surfaces;

namespace MinkQuickLax.Services;

/// <summary>Slides icon windows to new places together, frame by frame, then stops (SPEC 5.4 "tidy up").</summary>
public sealed class WindowMover
{
    private readonly Dictionary<IconWindow, Move> _moves = [];
    private readonly Stopwatch _clock = new();
    private bool _running;

    public void Animate(IconWindow window, PixelRect target, int dpi, TimeSpan duration)
    {
        _moves[window] = new Move(window.SquareRect, target, dpi, _clock.Elapsed, duration);
        if (!_running)
        {
            _clock.Start();
            CompositionTarget.Rendering += OnFrame;
            _running = true;
        }
    }

    /// <summary>Stops any slide of <paramref name="window"/> (for example when the user starts dragging it).</summary>
    public void Cancel(IconWindow window) => _moves.Remove(window);

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        foreach (var (window, move) in _moves.ToArray())
        {
            var t = Math.Clamp((now - move.Start) / move.Duration, 0, 1);
            var eased = Motion.Spring.Ease(t);
            var x = (int)Math.Round(move.From.Left + (move.To.Left - move.From.Left) * eased);
            var y = (int)Math.Round(move.From.Top + (move.To.Top - move.From.Top) * eased);
            window.MoveTo(new PixelRect(x, y, x + move.To.Width, y + move.To.Height), move.Dpi);
            if (t >= 1)
            {
                window.MoveTo(move.To, move.Dpi);
                _moves.Remove(window);
            }
        }
        if (_moves.Count == 0)
        {
            CompositionTarget.Rendering -= OnFrame;
            _clock.Reset();
            _running = false;
        }
    }

    private sealed record Move(PixelRect From, PixelRect To, int Dpi, TimeSpan Start, TimeSpan Duration);
}
