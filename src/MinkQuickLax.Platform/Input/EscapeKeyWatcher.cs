using MinkQuickLax.Platform.Interop;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MinkQuickLax.Platform.Input;

/// <summary>
/// Sees Esc while one of our popups is open. Our windows never take focus, so Esc is registered as a hotkey
/// only for as long as a popup is shown and released right after, giving the key back to other apps.
/// </summary>
public sealed class EscapeKeyWatcher : IDisposable
{
    private const int HotkeyId = 1;

    private readonly MessageWindow _window;
    private bool _registered;

    public EscapeKeyWatcher()
    {
        _window = MessageWindow.CreateMessageOnly("MinkQuickLax.EscapeKey");
        _window.MessageReceived += OnMessage;
    }

    public event Action? Pressed;

    /// <summary>Starts catching Esc. Returns false when another app already holds it.</summary>
    public bool Start()
    {
        if (!_registered)
        {
            _registered = PInvoke.RegisterHotKey(_window.Hwnd, HotkeyId, HOT_KEY_MODIFIERS.MOD_NOREPEAT, (uint)VIRTUAL_KEY.VK_ESCAPE);
        }
        return _registered;
    }

    public void Stop()
    {
        if (_registered)
        {
            PInvoke.UnregisterHotKey(_window.Hwnd, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _window.Dispose();
    }

    private nint? OnMessage(WindowMessage message)
    {
        if (message.Id == PInvoke.WM_HOTKEY && message.WParam == HotkeyId)
        {
            Pressed?.Invoke();
            return 0;
        }
        return null;
    }
}
