using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace SpikeLab;

/// <summary>A normal window with a TextBox, standing in for Notepad in the focus test.</summary>
internal static class FocusTarget
{
    public static async Task<int> RunAsync(string[] args)
    {
        Report.Name = "focus-target";
        var resultFile = args[0];
        var box = new TextBox { FontSize = 20, Margin = new Thickness(20) };
        var window = new Window { Title = "Spike focus target", Width = 420, Height = 200, Left = 80, Top = 300, Content = box };
        var deactivated = 0;
        window.Deactivated += (_, _) => deactivated++;
        var closed = new TaskCompletionSource();
        var atClose = 0;
        window.Closing += (_, _) => atClose = deactivated;
        window.Closed += (_, _) =>
        {
            File.WriteAllText(resultFile, $"text={box.Text} deactivated={atClose}");
            closed.TrySetResult();
        };
        window.Show();
        await Task.Delay(300);
        var hwnd = Native.Hwnd(window);
        var p = box.PointToScreen(new Point(box.ActualWidth / 2, box.ActualHeight / 2));
        File.WriteAllText(resultFile + ".ready", $"{(nint)hwnd},{(int)p.X},{(int)p.Y}");
        await closed.Task;
        return 0;
    }
}

/// <summary>Moves the mouse along a zigzag at ~125 Hz from a separate process, so it does not skew CPU numbers.</summary>
internal static class MouseDriver
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint period);

    public static int Run(string[] args)
    {
        var (left, top, right, bottom, seconds) = (int.Parse(args[0]), int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]));
        timeBeginPeriod(1);
        try
        {
            var sw = Stopwatch.StartNew();
            var next = 0.0;
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                var t = sw.Elapsed.TotalSeconds;
                // Sweep horizontally every 2 s while drifting vertically every 7 s.
                var fx = 0.5 - 0.5 * Math.Cos(t * Math.PI);
                var fy = 0.5 - 0.5 * Math.Cos(t * Math.PI * 2 / 7);
                Native.MouseMove(left + (int)((right - left) * fx), top + (int)((bottom - top) * fy));
                next += 8;
                var wait = next - sw.Elapsed.TotalMilliseconds;
                if (wait > 0)
                {
                    Thread.Sleep((int)wait);
                }
            }
        }
        finally
        {
            timeEndPeriod(1);
        }
        return 0;
    }
}
