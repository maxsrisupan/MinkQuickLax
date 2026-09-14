using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.ProcessStatus;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SpikeLab;

internal static class Native
{
    public static HWND Hwnd(Window w) => (HWND)new WindowInteropHelper(w).EnsureHandle();

    public static void AddExStyles(Window w, WINDOW_EX_STYLE add)
    {
        var hwnd = Hwnd(w);
        var ex = (WINDOW_EX_STYLE)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        ex |= add;
        ex &= ~WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)ex);
    }

    public static WINDOW_EX_STYLE ExStyle(HWND hwnd) =>
        (WINDOW_EX_STYLE)PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

    public static void MoveTo(HWND hwnd, int x, int y) =>
        PInvoke.SetWindowPos(hwnd, HWND.Null, x, y, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

    public static RECT Rect(HWND hwnd)
    {
        PInvoke.GetWindowRect(hwnd, out var r);
        return r;
    }

    public static System.Drawing.Point Cursor()
    {
        PInvoke.GetCursorPos(out var p);
        return new System.Drawing.Point(p.X, p.Y);
    }

    // ---- input simulation (physical pixels on the primary monitor) ----

    public static unsafe void MouseMove(int x, int y)
    {
        var w = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var h = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dx = (int)Math.Round(x * 65535.0 / (w - 1));
        input.Anonymous.mi.dy = (int)Math.Round(y * 65535.0 / (h - 1));
        input.Anonymous.mi.dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE;
        PInvoke.SendInput([input], sizeof(INPUT));
    }

    public static unsafe void MouseButton(bool down, bool right = false)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi.dwFlags = (down, right) switch
        {
            (true, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
            (false, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
            (true, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
            _ => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
        };
        PInvoke.SendInput([input], sizeof(INPUT));
    }

    public static async Task Click(int x, int y, bool right = false)
    {
        MouseMove(x, y);
        await Task.Delay(60);
        MouseButton(true, right);
        await Task.Delay(40);
        MouseButton(false, right);
        await Task.Delay(60);
    }

    public static unsafe void TypeText(string text)
    {
        foreach (var ch in text)
        {
            var down = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
            down.Anonymous.ki.wScan = ch;
            down.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE;
            var up = down;
            up.Anonymous.ki.dwFlags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
            PInvoke.SendInput([down, up], sizeof(INPUT));
        }
    }

    public static unsafe void Key(VIRTUAL_KEY vk)
    {
        var down = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        down.Anonymous.ki.wVk = vk;
        var up = down;
        up.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        PInvoke.SendInput([down, up], sizeof(INPUT));
    }

    // ---- measurement ----

    public static unsafe (double privateMb, double privateWorkingSetMb, double workingSetMb) Memory()
    {
        var c = new PROCESS_MEMORY_COUNTERS_EX2 { cb = (uint)sizeof(PROCESS_MEMORY_COUNTERS_EX2) };
        PInvoke.K32GetProcessMemoryInfo(PInvoke.GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&c, c.cb);
        const double Mb = 1024 * 1024;
        return (c.PrivateUsage / Mb, c.PrivateWorkingSetSize / Mb, c.WorkingSetSize / Mb);
    }
}

/// <summary>CPU time of this process over a wall-clock window.</summary>
internal sealed class CpuMeter
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _cpu;
    private readonly Stopwatch _wall = new();

    public CpuMeter Start()
    {
        _process.Refresh();
        _cpu = _process.TotalProcessorTime;
        _wall.Restart();
        return this;
    }

    /// <summary>Returns (% of all logical cores like Task Manager, % of one core).</summary>
    public (double total, double oneCore) Stop()
    {
        _process.Refresh();
        var cpu = (_process.TotalProcessorTime - _cpu).TotalMilliseconds;
        var wall = _wall.Elapsed.TotalMilliseconds;
        var oneCore = cpu / wall * 100;
        return (oneCore / Environment.ProcessorCount, oneCore);
    }
}
