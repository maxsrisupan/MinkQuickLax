using Microsoft.Win32;
using MinkQuickLax.Platform.SystemIntegration;

namespace MinkQuickLax.Platform.Tests;

/// <summary>Runs against a throwaway key under HKCU\Software\MinkQuickLax.Tests, never the real Run key.</summary>
public sealed class StartupRegistrationTests : IDisposable
{
    private const string Value = "MinkQuickLax";
    private readonly string _root = @"Software\MinkQuickLax.Tests\" + Guid.NewGuid().ToString("N");
    private readonly StartupRegistration _startup;

    public StartupRegistrationTests()
    {
        _startup = new StartupRegistration(_root + @"\Run", _root + @"\StartupApproved", Value);
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(@"Software\MinkQuickLax.Tests");
        if (parent is { SubKeyCount: 0, ValueCount: 0 })
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\MinkQuickLax.Tests", throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void NothingRegistered_IsOff()
    {
        Assert.Equal(StartupState.Off, _startup.Read());
        Assert.Null(_startup.Command());
    }

    [Fact]
    public void Enable_WritesAQuotedCommandWithTheStartupArgument()
    {
        _startup.Enable(@"C:\Users\me\AppData\Local\MinkQuickLax\current\MinkQuickLax.exe");

        Assert.Equal(StartupState.On, _startup.Read());
        Assert.Equal("\"C:\\Users\\me\\AppData\\Local\\MinkQuickLax\\current\\MinkQuickLax.exe\" --startup", _startup.Command());
    }

    [Theory]
    [InlineData((byte)3, StartupState.DisabledInTaskManager)]
    [InlineData((byte)7, StartupState.DisabledInTaskManager)]
    [InlineData((byte)2, StartupState.On)]
    [InlineData((byte)6, StartupState.On)]
    public void TaskManagerSwitch_IsRespected(byte marker, StartupState expected)
    {
        _startup.Enable(@"C:\app.exe");
        SetApproved([marker, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(expected, _startup.Read());
    }

    [Fact]
    public void EnablingAgain_ClearsTheTaskManagerDisable()
    {
        _startup.Enable(@"C:\app.exe");
        SetApproved([3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8]);

        _startup.Enable(@"C:\app.exe");

        Assert.Equal(StartupState.On, _startup.Read());
    }

    [Fact]
    public void Disable_RemovesBothValues()
    {
        _startup.Enable(@"C:\app.exe");
        SetApproved([3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

        _startup.Disable();

        Assert.Equal(StartupState.Off, _startup.Read());
        using var approved = Registry.CurrentUser.OpenSubKey(_root + @"\StartupApproved");
        Assert.Null(approved?.GetValue(Value));
    }

    private void SetApproved(byte[] marker)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_root + @"\StartupApproved");
        key.SetValue(Value, marker, RegistryValueKind.Binary);
    }
}
