using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MinkQuickLax.Platform.Input;

public enum MouseButton
{
    Left,
    Right,
    Middle,
}

/// <summary>
/// Follows the mouse anywhere on screen through Raw Input (no hooks, so it cannot slow the cursor).
/// Raises <see cref="Moved"/> at most every 33 ms, always including the final position, and
/// <see cref="ButtonDown"/> immediately. No timer runs while the mouse is still.
/// </summary>
public sealed unsafe class MouseProximityTracker : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);

    private const nuint ThrottleTimerId = 1;
    private const ushort GenericDesktopPage = 0x01;
    private const ushort MouseUsage = 0x02;
    private const int LeftDown = 0x0001;
    private const int RightDown = 0x0004;
    private const int MiddleDown = 0x0010;

    private readonly MessageWindow _window;
    private bool _dirty;
    private bool _timerRunning;

    public MouseProximityTracker()
    {
        _window = MessageWindow.CreateMessageOnly("MinkQuickLax.RawInput");
        _window.MessageReceived += OnMessage;
        var device = new RAWINPUTDEVICE
        {
            usUsagePage = GenericDesktopPage,
            usUsage = MouseUsage,
            dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK,
            hwndTarget = _window.Hwnd,
        };
        if (!PInvoke.RegisterRawInputDevices([device], (uint)sizeof(RAWINPUTDEVICE)))
        {
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError(), "RegisterRawInputDevices failed");
        }
    }

    /// <summary>Cursor position in physical pixels.</summary>
    public event Action<PixelPoint>? Moved;

    public event Action<PixelPoint, MouseButton>? ButtonDown;

    public static PixelPoint CursorPosition()
    {
        PInvoke.GetCursorPos(out var p);
        return new PixelPoint(p.X, p.Y);
    }

    public void Dispose()
    {
        var device = new RAWINPUTDEVICE { usUsagePage = GenericDesktopPage, usUsage = MouseUsage, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE };
        PInvoke.RegisterRawInputDevices([device], (uint)sizeof(RAWINPUTDEVICE));
        StopTimer();
        _window.Dispose();
    }

    private nint? OnMessage(WindowMessage message)
    {
        if (message.Id == PInvoke.WM_INPUT)
        {
            HandleInput((HRAWINPUT)message.LParam);
            return null; // DefWindowProc must still run for WM_INPUT.
        }
        if (message.Id == PInvoke.WM_TIMER && message.WParam == ThrottleTimerId)
        {
            if (_dirty)
            {
                RaiseMoved();
            }
            else
            {
                StopTimer();
            }
            return 0;
        }
        return null;
    }

    private void HandleInput(HRAWINPUT handle)
    {
        uint size = 0;
        var header = (uint)sizeof(RAWINPUTHEADER);
        _ = PInvoke.GetRawInputData(handle, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, null, &size, header);
        if (size == 0 || size > 1024)
        {
            return;
        }
        var buffer = stackalloc byte[(int)size];
        if (PInvoke.GetRawInputData(handle, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, buffer, &size, header) == unchecked((uint)-1))
        {
            return;
        }
        var raw = (RAWINPUT*)buffer;
        const uint RimTypeMouse = 0;
        if (raw->header.dwType != RimTypeMouse)
        {
            return;
        }

        var buttons = raw->data.mouse.Anonymous.Anonymous.usButtonFlags;
        if ((buttons & (LeftDown | RightDown | MiddleDown)) != 0)
        {
            var at = CursorPosition();
            if ((buttons & LeftDown) != 0)
            {
                ButtonDown?.Invoke(at, MouseButton.Left);
            }
            if ((buttons & RightDown) != 0)
            {
                ButtonDown?.Invoke(at, MouseButton.Right);
            }
            if ((buttons & MiddleDown) != 0)
            {
                ButtonDown?.Invoke(at, MouseButton.Middle);
            }
        }

        _dirty = true;
        if (!_timerRunning)
        {
            RaiseMoved();
            PInvoke.SetTimer(_window.Hwnd, ThrottleTimerId, (uint)Interval.TotalMilliseconds, null);
            _timerRunning = true;
        }
    }

    private void RaiseMoved()
    {
        _dirty = false;
        Moved?.Invoke(CursorPosition());
    }

    private void StopTimer()
    {
        if (_timerRunning)
        {
            PInvoke.KillTimer(_window.Hwnd, ThrottleTimerId);
            _timerRunning = false;
        }
    }
}

public static class Keyboard
{
    /// <summary>Reads the physical key state; works while none of our windows has focus.</summary>
    public static bool IsCtrlDown => (PInvoke.GetKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL) & 0x8000) != 0;

    public static bool IsShiftDown => (PInvoke.GetKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0;
}
