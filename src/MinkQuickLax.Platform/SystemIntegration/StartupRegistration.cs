using Microsoft.Win32;

namespace MinkQuickLax.Platform.SystemIntegration;

public enum StartupState
{
    Off,
    On,

    /// <summary>Registered, but the user turned it off in Task Manager's Startup apps.</summary>
    DisabledInTaskManager,
}

/// <summary>
/// Start at sign-in through HKCU\...\Run (SPEC 4.10). Task Manager keeps its own on/off switch under
/// Explorer\StartupApproved\Run, so the real state is read from both keys every time and never cached.
/// </summary>
public sealed class StartupRegistration(string runKeyPath, string approvedKeyPath, string valueName)
{
    public const string StartupArgument = "--startup";

    // Task Manager writes 12 bytes; an odd first byte means "disabled".
    private static readonly byte[] EnabledMarker = [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public static StartupRegistration ForCurrentUser() => new(
        @"Software\Microsoft\Windows\CurrentVersion\Run",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
        "MinkQuickLax");

    public StartupState Read()
    {
        if (Command() is null)
        {
            return StartupState.Off;
        }
        using var approved = Registry.CurrentUser.OpenSubKey(approvedKeyPath);
        return approved?.GetValue(valueName) is byte[] { Length: > 0 } marker && (marker[0] & 1) == 1
            ? StartupState.DisabledInTaskManager
            : StartupState.On;
    }

    /// <summary>The registered command line, or null when not registered.</summary>
    public string? Command()
    {
        using var run = Registry.CurrentUser.OpenSubKey(runKeyPath);
        return run?.GetValue(valueName) is string { Length: > 0 } command ? command : null;
    }

    /// <summary>Registers <paramref name="exePath"/>; an explicit "on" from the user also clears Task Manager's "disabled".</summary>
    public void Enable(string exePath)
    {
        using (var run = Registry.CurrentUser.CreateSubKey(runKeyPath))
        {
            run.SetValue(valueName, $"\"{exePath}\" {StartupArgument}", RegistryValueKind.String);
        }
        using var approved = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: true);
        if (approved?.GetValue(valueName) is byte[] { Length: > 0 } marker && (marker[0] & 1) == 1)
        {
            approved.SetValue(valueName, EnabledMarker, RegistryValueKind.Binary);
        }
    }

    public void Disable()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true))
        {
            run?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        using var approved = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: true);
        approved?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
