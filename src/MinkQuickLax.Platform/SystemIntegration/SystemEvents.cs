using System.Runtime.InteropServices;
using Microsoft.Win32;
using MinkQuickLax.Platform.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MinkQuickLax.Platform.SystemIntegration;

/// <summary>
/// Listens for display, work-area, DPI, power and Windows settings changes on a hidden top-level window
/// (message-only windows do not receive these broadcasts). Create on the UI thread.
/// </summary>
public sealed unsafe class SystemEvents : IDisposable
{
    private readonly MessageWindow _window;
    private readonly HPOWERNOTIFY _powerSavingNotification;
    private readonly uint _taskbarCreated;

    public SystemEvents()
    {
        _window = MessageWindow.CreateHiddenTopLevel("MinkQuickLax.SystemEvents");
        _window.MessageReceived += OnMessage;
        _taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
        var guid = PInvoke.GUID_POWER_SAVING_STATUS;
        _powerSavingNotification = PInvoke.RegisterPowerSettingNotification((HANDLE)_window.Handle, &guid, REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    /// <summary>Resolution, monitor layout, DPI or work area (taskbar) changed, or the PC resumed.</summary>
    public event Action? DisplaysChanged;

    /// <summary>Theme, transparency, animation, high contrast or battery saver may have changed.</summary>
    public event Action? SettingsChanged;

    /// <summary>Explorer restarted; tray icons must be added again.</summary>
    public event Action? TaskbarCreated;

    public void Dispose()
    {
        if (!_powerSavingNotification.IsNull)
        {
            PInvoke.UnregisterPowerSettingNotification(_powerSavingNotification);
        }
        _window.Dispose();
    }

    private nint? OnMessage(WindowMessage message)
    {
        switch (message.Id)
        {
            case PInvoke.WM_DISPLAYCHANGE:
            case PInvoke.WM_DPICHANGED:
                DisplaysChanged?.Invoke();
                break;
            case PInvoke.WM_SETTINGCHANGE:
                if (message.WParam == (nuint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA)
                {
                    DisplaysChanged?.Invoke();
                }
                SettingsChanged?.Invoke();
                break;
            case PInvoke.WM_POWERBROADCAST:
                if (message.WParam == PInvoke.PBT_APMRESUMEAUTOMATIC)
                {
                    DisplaysChanged?.Invoke();
                }
                else if (message.WParam == PInvoke.PBT_POWERSETTINGCHANGE)
                {
                    SettingsChanged?.Invoke();
                }
                break;
            default:
                if (message.Id == _taskbarCreated)
                {
                    TaskbarCreated?.Invoke();
                }
                break;
        }
        return null;
    }
}

/// <summary>A snapshot of Windows settings that change how the glass surfaces look (SPEC 5.1, 5.4, 5.5).</summary>
public sealed record SystemLook(bool AppsUseLightTheme, bool TransparencyEnabled, bool AnimationsEnabled, bool HighContrast, bool BatterySaver);

public static unsafe class SystemSettings
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static SystemLook Read() => new(
        AppsUseLightTheme: ReadDword(PersonalizeKey, "AppsUseLightTheme", 1) != 0,
        TransparencyEnabled: ReadDword(PersonalizeKey, "EnableTransparency", 1) != 0,
        AnimationsEnabled: ReadAnimations(),
        HighContrast: ReadHighContrast(),
        BatterySaver: ReadBatterySaver());

    private static int ReadDword(string key, string name, int fallback)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(key);
            return k?.GetValue(name) is int value ? value : fallback;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return fallback;
        }
    }

    private static bool ReadAnimations()
    {
        BOOL enabled = true;
        return !PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, 0, &enabled, 0) || enabled;
    }

    private static bool ReadHighContrast()
    {
        var hc = new HIGHCONTRASTW { cbSize = (uint)sizeof(HIGHCONTRASTW) };
        return PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST, hc.cbSize, &hc, 0)
            && hc.dwFlags.HasFlag(HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON);
    }

    private static bool ReadBatterySaver() =>
        PInvoke.GetSystemPowerStatus(out var status) && status.SystemStatusFlag == 1;
}

/// <summary>Lets only one copy run per user session; later copies hand their request to the first one.</summary>
public sealed class SingleInstance : IDisposable
{
    private const uint AnyProcess = unchecked((uint)-1); // ASFW_ANY

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, bool isFirst, string pipeName)
    {
        _mutex = mutex;
        IsFirst = isFirst;
        _pipeName = pipeName;
    }

    /// <summary>A later copy asked for something (for example "show-settings"). Raised on a background thread.</summary>
    public event Action<string>? CommandReceived;

    public bool IsFirst { get; }

    public static SingleInstance Acquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: false, $@"Local\{name}", out _);
        bool isFirst;
        try
        {
            isFirst = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner crashed; we own it now.
            isFirst = true;
        }
        // Same name in every copy for this user and session; CurrentUserOnly keeps other users out.
        var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        return new SingleInstance(mutex, isFirst, $"{name}.{Environment.UserName}.{sessionId}");
    }

    /// <summary>Starts listening for commands from later copies (first instance only).</summary>
    public void Listen()
    {
        if (!IsFirst)
        {
            throw new InvalidOperationException("Only the first instance listens.");
        }
        _ = Task.Run(ListenLoopAsync);
    }

    /// <summary>Sends a command to the first instance. Returns false if it could not be reached.</summary>
    public bool Send(string command, TimeSpan timeout)
    {
        // The copy the user just started may bring a window forward; the running copy may not, unless allowed here.
        PInvoke.AllowSetForegroundWindow(AnyProcess);
        try
        {
            using var client = new System.IO.Pipes.NamedPipeClientStream(".", _pipeName, System.IO.Pipes.PipeDirection.Out, System.IO.Pipes.PipeOptions.CurrentUserOnly);
            client.Connect(timeout);
            using var writer = new StreamWriter(client);
            writer.WriteLine(command);
            writer.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        if (IsFirst)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Released on another thread already.
            }
        }
        _mutex.Dispose();
        _stop.Dispose();
    }

    private async Task ListenLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var server = new System.IO.Pipes.NamedPipeServerStream(_pipeName, System.IO.Pipes.PipeDirection.In, 1,
                    System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    CommandReceived?.Invoke(line.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client went away mid-message; keep listening.
            }
        }
    }
}
