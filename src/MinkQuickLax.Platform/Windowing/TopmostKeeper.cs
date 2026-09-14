using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace MinkQuickLax.Platform.Windowing;

/// <summary>
/// Puts our floating windows back on top whenever the foreground window changes, so another
/// always-on-top window cannot keep covering them (SPEC 6, 7). Create on the UI thread.
/// </summary>
public sealed class TopmostKeeper : IDisposable
{
    private readonly HashSet<nint> _windows = [];
    private readonly WINEVENTPROC _callback;
    private readonly HWINEVENTHOOK _hook;

    public TopmostKeeper()
    {
        // Keep the delegate alive for as long as the hook exists.
        _callback = OnForegroundChanged;
        _hook = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, HINSTANCE.Null, _callback, 0, 0, 0 /* WINEVENT_OUTOFCONTEXT */);
        if (_hook.IsNull)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError(), "SetWinEventHook failed");
        }
    }

    /// <summary>When false (for example while a full-screen app runs), windows are left where they are.</summary>
    public bool Enabled { get; set; } = true;

    public void Register(nint hwnd) => _windows.Add(hwnd);

    public void Unregister(nint hwnd) => _windows.Remove(hwnd);

    public void RaiseAll()
    {
        foreach (var hwnd in _windows)
        {
            WindowStyles.BringToTopmost(hwnd);
        }
    }

    public void Dispose() => PInvoke.UnhookWinEvent(_hook);

    private void OnForegroundChanged(HWINEVENTHOOK hook, uint @event, HWND hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (Enabled && !_windows.Contains(hwnd))
        {
            RaiseAll();
        }
    }
}
