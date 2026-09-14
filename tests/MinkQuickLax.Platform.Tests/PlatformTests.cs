using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Displays;
using MinkQuickLax.Platform.Shell;
using MinkQuickLax.Platform.SystemIntegration;

namespace MinkQuickLax.Platform.Tests;

public sealed class LauncherTests
{
    [Theory]
    [InlineData(2, LaunchError.NotFound)]
    [InlineData(3, LaunchError.NotFound)]
    [InlineData(53, LaunchError.NotFound)]
    [InlineData(1223, LaunchError.Cancelled)]
    [InlineData(5, LaunchError.AccessDenied)]
    [InlineData(740, LaunchError.AccessDenied)]
    [InlineData(1155, LaunchError.NoAssociation)]
    [InlineData(31, LaunchError.Other)]
    public void Win32Errors_MapToFriendlyKinds(int code, LaunchError expected)
    {
        Assert.Equal(expected, Launcher.FromWin32(code).Error);
    }

    [Fact]
    public void MissingTarget_IsDetected()
    {
        var missing = new Link { Kind = LinkKind.File, Target = @"C:\definitely\not\here\x.txt" };
        var present = new Link { Kind = LinkKind.Folder, Target = "%WINDIR%" };
        var url = new Link { Kind = LinkKind.Url, Target = "https://example.com" };

        Assert.True(Launcher.IsTargetMissing(missing));
        Assert.False(Launcher.IsTargetMissing(present));
        Assert.False(Launcher.IsTargetMissing(url));
    }

    [Fact]
    public void OpeningAMissingFile_ReportsNotFound()
    {
        var result = Launcher.Open(new Link { Kind = LinkKind.File, Target = @"C:\definitely\not\here\x.txt" });

        Assert.Equal(LaunchError.NotFound, result.Error);
    }

    [Fact]
    public void UnknownKind_IsUnsupported()
    {
        Assert.Equal(LaunchError.Unsupported, Launcher.Open(new Link { Kind = LinkKind.Unknown, Target = "x" }).Error);
    }

    [Theory]
    [InlineData(LinkKind.App, true, true)]
    [InlineData(LinkKind.Command, true, false)]
    [InlineData(LinkKind.File, false, true)]
    [InlineData(LinkKind.ShellApp, false, false)]
    [InlineData(LinkKind.Url, false, false)]
    public void MenuCapabilities_FollowTheSpec(LinkKind kind, bool runAsAdmin, bool openLocation)
    {
        var link = new Link { Kind = kind };

        Assert.Equal(runAsAdmin, Launcher.CanRunAsAdmin(link));
        Assert.Equal(openLocation, Launcher.CanOpenLocation(link));
    }
}

public sealed class IconExtractorTests
{
    [Fact]
    public void WindowsFolder_HasALargeIcon()
    {
        var icon = IconExtractor.ForLink(new Link { Kind = LinkKind.Folder, Target = "%WINDIR%" });

        Assert.NotNull(icon);
        Assert.Equal(256, icon.Width);
        Assert.Equal(icon.Width * icon.Height * 4, icon.Pixels.Length);
        Assert.Contains(icon.Pixels.Where((_, i) => i % 4 == 3), a => a == 255);
        Assert.Contains(icon.Pixels.Where((_, i) => i % 4 == 3), a => a == 0);
    }

    [Fact]
    public void UrlLinks_HaveNoShellIcon()
    {
        Assert.Null(IconExtractor.ForLink(new Link { Kind = LinkKind.Url, Target = "https://example.com" }));
    }

    [Fact]
    public void Framed_SmallIcon_IsRecognized()
    {
        const int Size = 256;
        var pixels = new byte[Size * Size * 4];
        void Set(int x, int y, byte a) => pixels[(y * Size + x) * 4 + 3] = a;
        for (var i = 0; i < Size; i++)
        {
            for (var t = 0; t < 2; t++)
            {
                Set(i, t, 255);
                Set(i, Size - 1 - t, 255);
                Set(t, i, 255);
                Set(Size - 1 - t, i, 255);
            }
        }
        for (var y = 112; y < 144; y++)
        {
            for (var x = 112; x < 144; x++)
            {
                Set(x, y, 255);
            }
        }

        Assert.True(IconExtractor.LooksFramed(new IconBitmap(Size, Size, pixels)));
    }

    [Fact]
    public void RealLargeIcon_IsNotFramed()
    {
        var icon = IconExtractor.ForLink(new Link { Kind = LinkKind.Folder, Target = "%WINDIR%" })!;

        Assert.False(IconExtractor.LooksFramed(icon));
    }
}

public sealed class MonitorProviderTests
{
    [Fact]
    public void ReturnsExactlyOnePrimaryMonitor_WithAWorkAreaInsideItsBounds()
    {
        var monitors = new MonitorProvider().GetMonitors();

        Assert.NotEmpty(monitors);
        var primary = Assert.Single(monitors, m => m.IsPrimary);
        Assert.True(primary.Bounds.Contains(primary.WorkArea));
        Assert.InRange(primary.Dpi, 96, 480);
        Assert.All(monitors, m => Assert.False(string.IsNullOrEmpty(m.Id)));
    }

    [Fact]
    public void EdidSerial_PrefersTheTextDescriptor()
    {
        var edid = new byte[128];
        BitConverter.GetBytes(123456u).CopyTo(edid, 12);
        Assert.Equal("123456", MonitorProvider.ParseSerial(edid));

        // Descriptor 2 at offset 72: 00 00 00 FF 00 then 13 characters.
        edid[72 + 3] = 0xFF;
        System.Text.Encoding.ASCII.GetBytes("7XK3LM2\n     ").CopyTo(edid, 72 + 5);
        Assert.Equal("7XK3LM2", MonitorProvider.ParseSerial(edid));
    }
}

public sealed class SingleInstanceTests
{
    [Fact]
    public async Task SecondCopy_IsNotFirst_AndItsCommandReachesTheFirst()
    {
        var name = "MinkQuickLax.Tests." + Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var first = await Task.Factory.StartNew(() => SingleInstance.Acquire(name), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Assert.True(first.IsFirst);
        first.CommandReceived += command => received.TrySetResult(command);
        first.Listen();

        var sent = await Task.Factory.StartNew(() =>
        {
            using var second = SingleInstance.Acquire(name);
            Assert.False(second.IsFirst);
            return second.Send("show-settings", TimeSpan.FromSeconds(5));
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        Assert.True(sent);
        Assert.Equal("show-settings", await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}
