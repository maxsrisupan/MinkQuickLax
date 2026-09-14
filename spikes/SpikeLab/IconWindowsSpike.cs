using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SpikeLab;

/// <summary>S1 (30 floating icon windows), S2 (custom drag loop + grid snap), S3 (raw input proximity).</summary>
internal static class IconWindowsSpike
{
    private const int Count = 30;
    private const double IconSize = 48;
    private const double Margin = 12;
    private const int Grid = 24;

    public static async Task<int> RunAsync(string[] args)
    {
        var shadow = !args.Contains("noshadow");
        IconWin.Fps = int.TryParse(args.FirstOrDefault(a => a.StartsWith("fps=", StringComparison.Ordinal))?[4..], out var fps) ? fps : 0;
        IconWin.Direct = args.Contains("direct");
        IconWin.SoftwarePerWindow = args.Contains("swwin");
        ProximityController.Smooth = args.Contains("smooth");
        if (args.Contains("sw"))
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        var only = args.FirstOrDefault(a => a.StartsWith("only=", StringComparison.Ordinal))?[5..];
        Report.Name = "s1-s3" + (shadow ? "" : "-noshadow") + (IconWin.Fps > 0 ? $"-fps{IconWin.Fps}" : "") + (args.Contains("sw") ? "-sw" : "") + (IconWin.Direct ? "-direct" : "") + (IconWin.SoftwarePerWindow ? "-swwin" : "") + (ProximityController.Smooth ? "-smooth" : "");
        Report.Line($"shadow effect: {shadow}");
        var work = SystemParameters.WorkArea;
        var icons = new List<IconWin>();
        for (var i = 0; i < Count; i++)
        {
            var col = i % 6;
            var row = i / 6;
            var w = new IconWin(LetterTile.Render(((char)('A' + i)).ToString(), i), shadow)
            {
                Left = work.Right - 6 * 84 - 40 + col * 84,
                Top = 40 + row * 84,
            };
            w.Show();
            icons.Add(w);
        }
        await Task.Delay(2500);
        Screenshot.Save(new Int32Rect((int)(work.Right - 6 * 84 - 60), 20, 6 * 84 + 40, 5 * 84 + 40), "s1-icons.png");

        if (only is null or "s1")
        {
            await S1(icons);
        }
        if (only is null or "s2")
        {
            await S2(icons[0], work);
        }
        if (only is null or "s3")
        {
            await S3(icons);
        }

        foreach (var w in icons)
        {
            w.Close();
        }
        return 0;
    }

    private static async Task S1(List<IconWin> icons)
    {
        Report.Line("=== S1 ===");
        var hwnds = icons.Select(i => i.Handle).ToList();
        var styled = hwnds.Count(h =>
        {
            var ex = Native.ExStyle(h);
            return ex.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) && ex.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE)
                && ex.HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST) && ex.HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED)
                && !ex.HasFlag(WINDOW_EX_STYLE.WS_EX_APPWINDOW);
        });
        Report.Line($"windows with TOOLWINDOW|NOACTIVATE|TOPMOST|LAYERED and no APPWINDOW: {styled}/{hwnds.Count} (tool windows are excluded from Alt+Tab and taskbar)");

        await Task.Delay(6000);
        var m1 = Native.Memory();
        Report.Line($"memory after settle: private={m1.privateMb:F1}MB privateWS={m1.privateWorkingSetMb:F1}MB WS={m1.workingSetMb:F1}MB");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(1000);
        var m2 = Native.Memory();
        Report.Line($"memory after GC: private={m2.privateMb:F1}MB privateWS={m2.privateWorkingSetMb:F1}MB WS={m2.workingSetMb:F1}MB");

        var cpu = new CpuMeter().Start();
        await Task.Delay(20000);
        var idle = cpu.Stop();
        Report.Line($"idle CPU 20s: {idle.total:F3}% of total ({idle.oneCore:F3}% of one core)");

        // Focus: a separate process with a TextBox stands in for Notepad.
        var resultFile = Path.Combine(Path.GetTempPath(), $"spike-focus-{Environment.ProcessId}.txt");
        var readyFile = resultFile + ".ready";
        File.Delete(resultFile);
        File.Delete(readyFile);
        using var target = Process.Start(Environment.ProcessPath!, ["target", resultFile]);
        await Report.WaitUntil(() => File.Exists(readyFile), 15000);
        await Task.Delay(500);
        var ready = File.ReadAllText(readyFile).Split(',');
        var targetHwnd = (HWND)nint.Parse(ready[0]);
        var (tx, ty) = (int.Parse(ready[1]), int.Parse(ready[2]));
        var cursorBefore = Native.Cursor();

        await Native.Click(tx, ty);
        await Task.Delay(400);
        Report.Line($"target is foreground after clicking it: {PInvoke.GetForegroundWindow() == targetHwnd}");
        Native.TypeText("abc");
        await Task.Delay(300);

        var icon = icons[7];
        var r = Native.Rect(icon.Handle);
        var clicksBefore = icon.Clicks;
        await Native.Click((r.left + r.right) / 2, (r.top + r.bottom) / 2);
        await Task.Delay(400);
        Report.Line($"icon received click: {icon.Clicks > clicksBefore}");
        Report.Line($"target still foreground after clicking icon: {PInvoke.GetForegroundWindow() == targetHwnd}");
        Native.TypeText("def");
        await Task.Delay(300);
        PInvoke.PostMessage(targetHwnd, PInvoke.WM_CLOSE, default, default);
        await Report.WaitUntil(() => File.Exists(resultFile), 10000);
        await Task.Delay(200);
        Report.Line($"target result: {File.ReadAllText(resultFile).Trim()} (expect text=abcdef deactivated=0)");
        Native.MouseMove(cursorBefore.X, cursorBefore.Y);
    }

    private static async Task S2(IconWin icon, Rect work)
    {
        Report.Line("=== S2 ===");
        icon.DragEnabled = true;
        icon.MoveSamples.Clear();
        var r0 = Native.Rect(icon.Handle);
        var (w0, h0) = (r0.right - r0.left, r0.bottom - r0.top);
        var (sx, sy) = ((r0.left + r0.right) / 2, (r0.top + r0.bottom) / 2);
        Native.MouseMove(sx, sy);
        await Task.Delay(100);
        Native.MouseButton(true);
        await Task.Delay(50);
        const int Steps = 120;
        var sw = Stopwatch.StartNew();
        for (var i = 1; i <= Steps; i++)
        {
            Native.MouseMove(sx - (int)(i * 3.1), sy + (int)(i * 1.7));
            await Task.Delay(8);
        }
        Native.MouseButton(false);
        await Task.Delay(300);
        var r1 = Native.Rect(icon.Handle);
        var centerX = (r1.left + r1.right) / 2 - (int)work.Left;
        var centerY = (r1.top + r1.bottom) / 2 - (int)work.Top;
        var samples = icon.MoveSamples.ToArray();
        Report.Line($"drag moves handled: {samples.Length} in {sw.ElapsedMilliseconds}ms; handler avg={samples.DefaultIfEmpty().Average():F3}ms max={samples.DefaultIfEmpty().Max():F3}ms");
        Report.Line($"final center relative to work area: ({centerX},{centerY}) snapped to {Grid}px: {centerX % Grid == 0 && centerY % Grid == 0}");
        Report.Line($"size before {w0}x{h0} after {r1.right - r1.left}x{r1.bottom - r1.top}");
        Report.Line($"window moved: {r1.left != r0.left || r1.top != r0.top}");
        icon.DragEnabled = false;
    }

    private static async Task S3(List<IconWin> icons)
    {
        Report.Line("=== S3 ===");
        using var tracker = new RawMouseTracker();
        var proximity = new ProximityController(icons);
        tracker.Moved += proximity.Update;

        var rects = icons.Select(i => Native.Rect(i.Handle)).ToList();
        var left = rects.Min(r => r.left) - 60;
        var top = rects.Min(r => r.top) - 20;
        var right = rects.Max(r => r.right) + 60;
        var bottom = rects.Max(r => r.bottom) + 20;
        var cursorBefore = Native.Cursor();

        var cpu = new CpuMeter().Start();
        using (var driver = Process.Start(Environment.ProcessPath!, ["drive", $"{left}", $"{top}", $"{right}", $"{bottom}", "20"]))
        {
            await Task.Delay(9000);
            Screenshot.Save(new Int32Rect(left - 20, top - 20, right - left + 40, bottom - top + 40), "s3-proximity.png");
            await driver.WaitForExitAsync();
        }
        var moving = cpu.Stop();
        Report.Line($"WM_INPUT received: {tracker.InputCount}, updates raised: {tracker.RaisedCount}, window updates: {proximity.WindowUpdates}");
        Report.Line($"WM_INPUT handler avg={tracker.AvgHandlerMs:F4}ms max={tracker.MaxHandlerMs:F3}ms");
        Report.Line($"CPU while moving ~20s: {moving.total:F2}% of total ({moving.oneCore:F1}% of one core)");

        Native.MouseMove(cursorBefore.X, cursorBefore.Y);
        await Task.Delay(1500);
        cpu.Start();
        await Task.Delay(10000);
        var after = cpu.Stop();
        Report.Line($"CPU idle after moving 10s: {after.total:F3}% of total ({after.oneCore:F2}% of one core)");
        var m = Native.Memory();
        Report.Line($"memory at end: private={m.privateMb:F1}MB privateWS={m.privateWorkingSetMb:F1}MB");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(3000);
        m = Native.Memory();
        Report.Line($"memory at end after GC: private={m.privateMb:F1}MB privateWS={m.privateWorkingSetMb:F1}MB");
    }
}

internal sealed class IconWin : Window
{
    private readonly FrameworkElement _visual;
    private readonly ScaleTransform _scale = new(1, 1);
    private (int X, int Y) _grab;
    private bool _dragging;

    public IconWin(ImageSource image, bool shadow)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = Height = 48 + 24;

        _visual = new Image
        {
            Source = image,
            Width = 48,
            Height = 48,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _scale,
            Opacity = 0.7,
            Effect = shadow ? new DropShadowEffect { Direction = 270, ShadowDepth = 8, BlurRadius = 18, Opacity = 0.7, Color = Colors.Black } : null,
        };
        Content = new Grid { Children = { _visual } };

        SourceInitialized += (_, _) =>
        {
            Handle = Native.Hwnd(this);
            if (SoftwarePerWindow)
            {
                HwndSource.FromHwnd(Handle).CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }
            Native.AddExStyles(this, WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
        };
        _visual.MouseLeftButtonDown += OnDown;
        _visual.MouseMove += OnMove;
        _visual.MouseLeftButtonUp += OnUp;
    }

    public HWND Handle { get; private set; }
    public int Clicks { get; private set; }
    public bool DragEnabled { get; set; }
    public List<double> MoveSamples { get; } = [];
    public double TargetOpacity { get; set; } = 0.7;
    public double TargetScale { get; set; } = 1;

    public static int Fps { get; set; }

    public static bool Direct { get; set; }
    public static bool SoftwarePerWindow { get; set; }
    public double CurrentOpacity { get; set; } = 0.7;
    public double CurrentScale { get; set; } = 1;

    public void SetNow(double opacity, double scale)
    {
        _visual.Opacity = opacity;
        _scale.ScaleX = _scale.ScaleY = scale;
    }

    public void AnimateTo(double opacity, double scale)
    {
        if (Direct)
        {
            _visual.Opacity = opacity;
            _scale.ScaleX = _scale.ScaleY = scale;
            return;
        }
        var duration = TimeSpan.FromMilliseconds(250);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _visual.BeginAnimation(OpacityProperty, Anim(opacity, duration, ease));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, Anim(scale, duration, ease));
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, Anim(scale, duration, ease));
    }

    private static DoubleAnimation Anim(double to, TimeSpan duration, IEasingFunction ease)
    {
        var a = new DoubleAnimation(to, duration) { EasingFunction = ease };
        if (Fps > 0)
        {
            Timeline.SetDesiredFrameRate(a, Fps);
        }
        return a;
    }

    private void OnDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!DragEnabled)
        {
            return;
        }
        var c = Native.Cursor();
        var r = Native.Rect(Handle);
        _grab = (c.X - r.left, c.Y - r.top);
        _dragging = true;
        _visual.CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        var t0 = Stopwatch.GetTimestamp();
        var c = Native.Cursor();
        var r = Native.Rect(Handle);
        var size = r.right - r.left;
        var work = SystemParameters.WorkArea;
        const int Grid = 24;
        var cx = c.X - _grab.X + size / 2 - (int)work.Left;
        var cy = c.Y - _grab.Y + size / 2 - (int)work.Top;
        cx = (int)Math.Round(cx / (double)Grid) * Grid;
        cy = (int)Math.Round(cy / (double)Grid) * Grid;
        var x = cx - size / 2 + (int)work.Left;
        var y = cy - size / 2 + (int)work.Top;
        if (x != r.left || y != r.top)
        {
            Native.MoveTo(Handle, x, y);
        }
        MoveSamples.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
    }

    private void OnUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            _visual.ReleaseMouseCapture();
        }
        else
        {
            Clicks++;
        }
    }
}

/// <summary>Maps cursor distance to opacity and scale; touches only icons that need to change.</summary>
internal sealed class ProximityController
{
    private const double Radius = 130;
    private const double IdleOpacity = 0.7;
    private const double MaxScale = 1.16;
    // Per 33 ms tick; reaches ~95% of the way in about 250 ms.
    private const double Step = 0.33;

    private readonly List<IconWin> _icons;
    private readonly DispatcherTimer _tick = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly HashSet<IconWin> _moving = [];

    public ProximityController(List<IconWin> icons)
    {
        _icons = icons;
        _tick.Tick += (_, _) => Advance();
    }

    public static bool Smooth { get; set; }
    public int WindowUpdates { get; private set; }
    public int Ticks { get; private set; }

    public void Update(int x, int y)
    {
        foreach (var icon in _icons)
        {
            var r = Native.Rect(icon.Handle);
            var dx = x - (r.left + r.right) / 2.0;
            var dy = y - (r.top + r.bottom) / 2.0;
            var t = Math.Clamp(1 - Math.Sqrt(dx * dx + dy * dy) / Radius, 0, 1);
            var opacity = IdleOpacity + (1 - IdleOpacity) * t;
            var scale = 1 + (MaxScale - 1) * t * t;
            if (Math.Abs(opacity - icon.TargetOpacity) < 0.01 && Math.Abs(scale - icon.TargetScale) < 0.005)
            {
                continue;
            }
            icon.TargetOpacity = opacity;
            icon.TargetScale = scale;
            if (Smooth)
            {
                _moving.Add(icon);
                if (!_tick.IsEnabled)
                {
                    _tick.Start();
                }
            }
            else
            {
                icon.AnimateTo(opacity, scale);
                WindowUpdates++;
            }
        }
    }

    private void Advance()
    {
        Ticks++;
        foreach (var icon in _moving.ToArray())
        {
            var o = icon.CurrentOpacity + (icon.TargetOpacity - icon.CurrentOpacity) * Step;
            var s = icon.CurrentScale + (icon.TargetScale - icon.CurrentScale) * Step;
            if (Math.Abs(icon.TargetOpacity - o) < 0.004 && Math.Abs(icon.TargetScale - s) < 0.002)
            {
                (o, s) = (icon.TargetOpacity, icon.TargetScale);
                _moving.Remove(icon);
            }
            icon.CurrentOpacity = o;
            icon.CurrentScale = s;
            icon.SetNow(o, s);
            WindowUpdates++;
        }
        if (_moving.Count == 0)
        {
            _tick.Stop();
        }
    }
}

/// <summary>Receives mouse movement from anywhere via Raw Input and raises at most 30 updates per second.</summary>
internal sealed class RawMouseTracker : IDisposable
{
    private readonly HwndSource _sink;
    private readonly DispatcherTimer _throttle;
    private bool _dirty;
    private double _totalHandlerMs;

    public unsafe RawMouseTracker()
    {
        _sink = new HwndSource(new HwndSourceParameters("SpikeRawInputSink") { ParentWindow = new nint(-3), WindowStyle = 0 });
        _sink.AddHook(Hook);
        var device = new RAWINPUTDEVICE
        {
            usUsagePage = 0x01,
            usUsage = 0x02,
            dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK,
            hwndTarget = (HWND)_sink.Handle,
        };
        if (!PInvoke.RegisterRawInputDevices([device], (uint)sizeof(RAWINPUTDEVICE)))
        {
            Report.Line($"RegisterRawInputDevices failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeError()}");
        }
        _throttle = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(33) };
        _throttle.Tick += (_, _) =>
        {
            if (_dirty)
            {
                Raise();
            }
            else
            {
                _throttle.Stop();
            }
        };
    }

    public event Action<int, int>? Moved;
    public int InputCount { get; private set; }
    public int RaisedCount { get; private set; }
    public double MaxHandlerMs { get; private set; }
    public double AvgHandlerMs => InputCount == 0 ? 0 : _totalHandlerMs / InputCount;

    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == PInvoke.WM_INPUT)
        {
            var t0 = Stopwatch.GetTimestamp();
            InputCount++;
            _dirty = true;
            if (!_throttle.IsEnabled)
            {
                Raise();
                _throttle.Start();
            }
            var ms = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
            _totalHandlerMs += ms;
            MaxHandlerMs = Math.Max(MaxHandlerMs, ms);
        }
        return 0;
    }

    private void Raise()
    {
        _dirty = false;
        RaisedCount++;
        var c = Native.Cursor();
        Moved?.Invoke(c.X, c.Y);
    }

    public unsafe void Dispose()
    {
        var device = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE };
        PInvoke.RegisterRawInputDevices([device], (uint)sizeof(RAWINPUTDEVICE));
        _throttle.Stop();
        _sink.Dispose();
    }
}

internal static class LetterTile
{
    private static readonly (string A, string B)[] Pairs =
    [
        ("#2FD9BE", "#1E8F9E"), ("#2F6FD9", "#1B3F8F"), ("#FFC15A", "#E08A1E"), ("#FF7250", "#C73E2A"),
        ("#FF5C8A", "#B8325F"), ("#8E6CF0", "#5A3FB8"), ("#7ED67A", "#3E9A4E"), ("#7F95A3", "#4B5F69"),
    ];

    public static BitmapSource Render(string letter, int index)
    {
        const int Px = 48;
        var (a, b) = Pairs[index % Pairs.Length];
        var ca = (Color)ColorConverter.ConvertFromString(a);
        var cb = (Color)ColorConverter.ConvertFromString(b);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var rect = new Rect(0, 0, Px, Px);
            var radius = Px * 0.29;
            var fill = new LinearGradientBrush(ca, cb, new Point(0.25, 0), new Point(0.75, 1));
            dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)), 1), rect, radius, radius);
            var gloss = new LinearGradientBrush(Color.FromArgb(77, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, Px, Px / 2.0), radius, radius));
            dc.DrawRoundedRectangle(gloss, null, rect, radius, radius);
            dc.Pop();
            var text = new FormattedText(letter, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), Px * 0.4, Brushes.White, 1.0);
            dc.DrawText(text, new Point((Px - text.Width) / 2, (Px - text.Height) / 2));
        }
        var bmp = new RenderTargetBitmap(Px, Px, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }
}

internal static class Screenshot
{
    public static void Save(Int32Rect r, string name)
    {
        using var bmp = new System.Drawing.Bitmap(r.Width, r.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(r.X, r.Y, 0, 0, bmp.Size);
        }
        Directory.CreateDirectory(Report.ResultsDir());
        bmp.Save(Path.Combine(Report.ResultsDir(), name), System.Drawing.Imaging.ImageFormat.Png);
    }
}
