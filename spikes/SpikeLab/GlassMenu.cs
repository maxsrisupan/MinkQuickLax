using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SpikeLab;

internal enum BackdropMode
{
    SystemBackdrop,
    SystemBackdropFakeActive,
    AccentAcrylic,
    Solid,
}

/// <summary>Glass context menu: non-layered, topmost, tool window, no activation.</summary>
internal sealed class GlassMenu : Window
{
    private readonly BackdropMode _mode;

    public GlassMenu(BackdropMode mode, string title, params string[] items)
    {
        _mode = mode;
        Title = title;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brushes.Transparent;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            GlassFrameThickness = new Thickness(-1),
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
            CornerRadius = default,
        });

        var alpha = mode == BackdropMode.Solid ? 0.92 : 0.58;
        var panel = new StackPanel { MinWidth = 210 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromArgb(168, 241, 250, 248)),
            FontSize = 11,
            Margin = new Thickness(10, 4, 10, 6),
        });
        foreach (var text in items)
        {
            var label = new TextBlock { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xFA, 0xF8)), FontSize = 13.5 };
            var item = new Border { Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, Child = label };
            item.MouseEnter += (_, _) => item.Background = new SolidColorBrush(Color.FromArgb(66, 255, 255, 255));
            item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
            item.MouseLeftButtonUp += (_, _) =>
            {
                Clicked = text;
                Close();
            };
            panel.Children.Add(item);
        }
        Content = new Border
        {
            Padding = new Thickness(6),
            Background = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 18, 30, 36)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = panel,
        };

        SourceInitialized += (_, _) => Apply();
    }

    public string? Clicked { get; private set; }
    public HWND Handle { get; private set; }

    private unsafe void Apply()
    {
        Handle = Native.Hwnd(this);
        Native.AddExStyles(this, WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
        HwndSource.FromHwnd(Handle).CompositionTarget.BackgroundColor = Colors.Transparent;

        int dark = 1;
        PInvoke.DwmSetWindowAttribute(Handle, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &dark, sizeof(int));
        var corner = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        PInvoke.DwmSetWindowAttribute(Handle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &corner, sizeof(int));

        switch (_mode)
        {
            case BackdropMode.SystemBackdrop:
            case BackdropMode.SystemBackdropFakeActive:
                var backdrop = (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW;
                var hr = PInvoke.DwmSetWindowAttribute(Handle, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &backdrop, sizeof(int));
                Report.Line($"{_mode}: DWMWA_SYSTEMBACKDROP_TYPE hr=0x{hr.Value:X8}");
                break;
            case BackdropMode.AccentAcrylic:
                var ok = Accent.EnableAcrylic(Handle, 0x60241E12);
                Report.Line($"{_mode}: SetWindowCompositionAttribute ok={ok}");
                break;
        }
    }

    public void FakeActivate()
    {
        PInvoke.SendMessage(Handle, PInvoke.WM_NCACTIVATE, 1, 0);
        PInvoke.DefWindowProc(Handle, PInvoke.WM_NCACTIVATE, 1, -1);
        Report.Line("fake activate: sent WM_NCACTIVATE and DefWindowProc(WM_NCACTIVATE, TRUE)");
    }
}

/// <summary>Undocumented accent policy used by the Windows 10 shell for acrylic.</summary>
internal static class Accent
{
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(nint hwnd, ref CompositionData data);

    public static unsafe bool EnableAcrylic(HWND hwnd, uint abgr)
    {
        var policy = new AccentPolicy { AccentState = 4, AccentFlags = 2, GradientColor = abgr };
        var data = new CompositionData { Attribute = 19, Data = (nint)(&policy), SizeOfData = sizeof(AccentPolicy) };
        return SetWindowCompositionAttribute(hwnd, ref data);
    }
}

/// <summary>Watches global mouse button presses through Raw Input (works while the app is never active).</summary>
internal sealed class RawButtonWatcher : IDisposable
{
    private readonly HwndSource _sink;

    public unsafe RawButtonWatcher()
    {
        _sink = new HwndSource(new HwndSourceParameters("SpikeRawButtons") { ParentWindow = new nint(-3), WindowStyle = 0 });
        _sink.AddHook(Hook);
        var device = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK, hwndTarget = (HWND)_sink.Handle };
        PInvoke.RegisterRawInputDevices([device], (uint)sizeof(RAWINPUTDEVICE));
    }

    public event Action<int, int>? ButtonDown;

    private unsafe nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != PInvoke.WM_INPUT)
        {
            return 0;
        }
        uint size = 0;
        var header = (uint)sizeof(RAWINPUTHEADER);
        PInvoke.GetRawInputData((HRAWINPUT)lParam, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, null, &size, header);
        var buffer = stackalloc byte[(int)size];
        if (PInvoke.GetRawInputData((HRAWINPUT)lParam, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, buffer, &size, header) == unchecked((uint)-1))
        {
            return 0;
        }
        var raw = (RAWINPUT*)buffer;
        if (raw->header.dwType == 0)
        {
            var flags = raw->data.mouse.Anonymous.Anonymous.usButtonFlags;
            const int AnyDown = 0x0001 | 0x0004 | 0x0010;
            if ((flags & AnyDown) != 0)
            {
                var c = Native.Cursor();
                ButtonDown?.Invoke(c.X, c.Y);
            }
        }
        return 0;
    }

    public unsafe void Dispose()
    {
        var device = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE };
        PInvoke.RegisterRawInputDevices([device], (uint)sizeof(RAWINPUTDEVICE));
        _sink.Dispose();
    }
}

internal static class PixelMetrics
{
    /// <summary>Mean absolute neighbor difference in grayscale; drops sharply when content is blurred.</summary>
    public static double Sharpness(System.Drawing.Bitmap bmp, System.Drawing.Rectangle r)
    {
        double sum = 0;
        var n = 0;
        for (var y = r.Top; y < r.Bottom - 1; y++)
        {
            for (var x = r.Left; x < r.Right - 1; x++)
            {
                var g = Gray(bmp.GetPixel(x, y));
                sum += Math.Abs(g - Gray(bmp.GetPixel(x + 1, y))) + Math.Abs(g - Gray(bmp.GetPixel(x, y + 1)));
                n++;
            }
        }
        return n == 0 ? 0 : sum / n;
    }

    private static double Gray(System.Drawing.Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

    public static System.Drawing.Bitmap Capture(int x, int y, int w, int h)
    {
        var bmp = new System.Drawing.Bitmap(w, h);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.CopyFromScreen(x, y, 0, 0, bmp.Size);
        return bmp;
    }
}
