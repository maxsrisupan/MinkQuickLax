using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MinkQuickLax.Platform.Interop;

/// <summary>A window message.</summary>
public readonly record struct WindowMessage(uint Id, nuint WParam, nint LParam);

/// <summary>
/// An invisible Win32 window that forwards messages to handlers. Message-only windows receive posted and
/// raw input messages; hidden top-level windows also receive broadcasts (display, settings, power).
/// Create and use on the UI thread; its messages are pumped by that thread's message loop.
/// </summary>
public sealed unsafe class MessageWindow : IDisposable
{
    private const string ClassName = "MinkQuickLax.MessageWindow";
    private static readonly HWND MessageOnlyParent = new(-3);
    private static readonly Dictionary<nint, MessageWindow> Live = [];

    // Kept in a static field so the delegate outlives every window of the class.
    private static readonly WNDPROC Procedure = WndProc;
    private static bool _classRegistered;

    private MessageWindow(HWND handle)
    {
        Handle = handle;
    }

    /// <summary>Handlers run in order; return a value to mark the message handled.</summary>
    public event Func<WindowMessage, nint?>? MessageReceived;

    /// <summary>Called when a handler throws; the exception is swallowed so it cannot unwind into native code.</summary>
    public static Action<Exception>? UnhandledException { get; set; }

    public nint Handle { get; }

    internal HWND Hwnd => (HWND)Handle;

    public static MessageWindow CreateMessageOnly(string name) => Create(name, messageOnly: true);

    public static MessageWindow CreateHiddenTopLevel(string name) => Create(name, messageOnly: false);

    public void Dispose()
    {
        if (Live.Remove(Handle))
        {
            PInvoke.DestroyWindow(Hwnd);
        }
    }

    private static MessageWindow Create(string name, bool messageOnly)
    {
        var instance = PInvoke.GetModuleHandle((char*)null);
        fixed (char* className = ClassName)
        fixed (char* title = name)
        {
            if (!_classRegistered)
            {
                var wc = new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = Procedure,
                    hInstance = (HINSTANCE)instance.Value,
                    lpszClassName = className,
                };
                if (PInvoke.RegisterClassEx(wc) == 0)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "RegisterClassEx failed");
                }
                _classRegistered = true;
            }

            var hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                className,
                title,
                WINDOW_STYLE.WS_POPUP,
                0,
                0,
                0,
                0,
                messageOnly ? MessageOnlyParent : HWND.Null,
                HMENU.Null,
                (HINSTANCE)instance.Value,
                null);
            if (hwnd.IsNull)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "CreateWindowEx failed");
            }
            var window = new MessageWindow(hwnd);
            Live[hwnd] = window;
            return window;
        }
    }

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (Live.TryGetValue(hwnd, out var window) && window.MessageReceived is { } handlers)
            {
                var message = new WindowMessage(msg, wParam.Value, lParam.Value);
                foreach (var handler in handlers.GetInvocationList().Cast<Func<WindowMessage, nint?>>())
                {
                    if (handler(message) is { } result)
                    {
                        return (LRESULT)result;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnhandledException?.Invoke(ex);
        }
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
