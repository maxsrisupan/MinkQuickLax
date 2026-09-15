using System.Runtime.InteropServices;
using MinkQuickLax.Core.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MinkQuickLax.Platform.Windowing;

/// <summary>How a floating surface behaves (PLAN.md 3.2).</summary>
[Flags]
public enum SurfaceBehavior
{
    None = 0,

    /// <summary>Clicking does not take focus from the app in use.</summary>
    NoActivate = 1,

    /// <summary>Mouse clicks pass through to whatever is below.</summary>
    ClickThrough = 2,
}

/// <summary>The one place where window styles of our surfaces are set.</summary>
public static class WindowStyles
{
    /// <summary>Topmost tool window: never in the taskbar or Alt+Tab.</summary>
    public static void ApplyFloating(nint hwnd, SurfaceBehavior behavior)
    {
        var handle = (HWND)hwnd;
        var ex = (WINDOW_EX_STYLE)PInvoke.GetWindowLongPtr(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        ex |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TOPMOST;
        ex &= ~WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        ex = behavior.HasFlag(SurfaceBehavior.NoActivate) ? ex | WINDOW_EX_STYLE.WS_EX_NOACTIVATE : ex & ~WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        ex = behavior.HasFlag(SurfaceBehavior.ClickThrough) ? ex | WINDOW_EX_STYLE.WS_EX_TRANSPARENT : ex & ~WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
        PInvoke.SetWindowLongPtr(handle, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)ex);
    }

    public static bool HasExStyle(nint hwnd, uint style) =>
        ((uint)PInvoke.GetWindowLongPtr((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) & style) == style;

    /// <summary>Moves and sizes in physical pixels without activating or changing Z order.</summary>
    public static void SetBounds(nint hwnd, PixelRect rect) =>
        PInvoke.SetWindowPos((HWND)hwnd, HWND.Null, rect.Left, rect.Top, rect.Width, rect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    /// <summary>Moves in physical pixels, keeping the size.</summary>
    public static void SetPosition(nint hwnd, int x, int y) =>
        PInvoke.SetWindowPos((HWND)hwnd, HWND.Null, x, y, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    public static void BringToTopmost(nint hwnd) =>
        PInvoke.SetWindowPos((HWND)hwnd, new HWND(-1) /* HWND_TOPMOST */, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    public static PixelRect GetBounds(nint hwnd)
    {
        PInvoke.GetWindowRect((HWND)hwnd, out var r);
        return new PixelRect(r.left, r.top, r.right, r.bottom);
    }

    public static int GetDpi(nint hwnd)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            return 96;
        }
        var dpi = PInvoke.GetDpiForWindow((HWND)hwnd);
        return dpi == 0 ? 96 : (int)dpi;
    }
}

/// <summary>Blur and rounded corners for non-layered glass surfaces (PLAN.md 3.2, spike S4).</summary>
public static unsafe class DwmBackdrop
{
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int AccentDisabled = 0;
    private const int WcaAccentPolicy = 19;

    /// <summary>Windows 11 22H2 (build 22621) or later: blur is offered (SPEC 2, 5.5).</summary>
    public static bool IsBlurSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    // DWMWA_COLOR_NONE: no system border line around the window.
    private const uint ColorNone = 0xFFFFFFFE;

    /// <summary>
    /// Windows 11 frame: rounded 8 px corners (Glass) or square corners so the content can draw its own shape (HUD cut
    /// corners, Dot Matrix plates). Either way without the system border line: every style draws its own edge, and the
    /// system's grey line would cover the Glass rim light (SPEC 5.5). No effect on Windows 10.
    /// </summary>
    public static void SetRoundedCorners(nint hwnd, bool rounded = true)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }
        var preference = (int)(rounded ? DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND : DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND);
        PInvoke.DwmSetWindowAttribute((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &preference, sizeof(int));
        var border = ColorNone;
        PInvoke.DwmSetWindowAttribute((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, &border, sizeof(uint));
    }

    /// <summary>System acrylic for normal windows that can be active (the scanner). Returns false when unsupported.</summary>
    public static bool SetSystemAcrylic(nint hwnd, bool enabled)
    {
        if (!IsBlurSupported)
        {
            return false;
        }
        var type = (int)(enabled ? DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW : DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE);
        return PInvoke.DwmSetWindowAttribute((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &type, sizeof(int)).Succeeded;
    }

    public static void SetDarkFrame(nint hwnd, bool dark)
    {
        var value = dark ? 1 : 0;
        PInvoke.DwmSetWindowAttribute((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &value, sizeof(int));
    }

    /// <summary>Turns acrylic blur on or off. Returns false when Windows refused, so the caller uses a solid fill.</summary>
    public static bool SetAcrylic(nint hwnd, bool enabled)
    {
        var policy = new AccentPolicy
        {
            AccentState = enabled ? AccentEnableAcrylicBlurBehind : AccentDisabled,
            AccentFlags = 2,
            // Nearly transparent; the glass tint is drawn by the window content (SPEC 5.2).
            GradientColor = 0x01000000,
        };
        var data = new CompositionAttributeData { Attribute = WcaAccentPolicy, Data = (nint)(&policy), SizeOfData = sizeof(AccentPolicy) };
        return SetWindowCompositionAttribute(hwnd, ref data);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    // Undocumented; CsWin32 has no metadata for it (PLAN.md 5 allows DllImport in this case).
    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowCompositionAttribute(nint hwnd, ref CompositionAttributeData data);
}
