using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Shell;

namespace MinkQuickLax.Platform.Tests;

public sealed class AppScannerTests
{
    [Theory]
    [InlineData("Uninstall Foo", @"C:\Foo\foo.exe", true)]
    [InlineData("Foo", @"C:\Program Files\Foo\unins000.exe", true)]
    [InlineData("Foo Setup", @"C:\Foo\uninstall.exe", true)]
    [InlineData("Visual Studio Code", @"C:\Code\Code.exe", false)]
    public void Uninstallers_AreLeftOut(string name, string target, bool expected)
    {
        Assert.Equal(expected, AppScanner.IsUninstaller(name, target));
    }

    [Fact]
    public void Scan_FindsStartMenuApps_WithUsableTargets()
    {
        var apps = AppScanner.Scan(TestContext.Current.CancellationToken);

        Assert.NotEmpty(apps);
        Assert.All(apps, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
        Assert.All(apps, a => Assert.False(string.IsNullOrWhiteSpace(a.Target)));
        Assert.All(apps.Where(a => a.Source == ScanSource.Store), a => Assert.Equal(LinkKind.ShellApp, a.Kind));
    }
}

public sealed class FaviconFetcherTests
{
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, true)]
    [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x10, 0x10, 0 }, true)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0 }, true)]
    [InlineData(new byte[] { (byte)'<', (byte)'!', (byte)'d', (byte)'o', (byte)'c', (byte)'t', (byte)'y', (byte)'p', (byte)'e' }, false)]
    [InlineData(new byte[] { 0x89, 0x50 }, false)]
    public void OnlyRealImagesAreAccepted(byte[] bytes, bool expected)
    {
        Assert.Equal(expected, FaviconFetcher.LooksLikeImage(bytes));
    }

    [Theory]
    [InlineData("https://github.com/")]
    [InlineData("https://www.wikipedia.org/")]
    public async Task RealSites_GiveAnIcon(string url)
    {
        if (Environment.GetEnvironmentVariable("MINKQUICKLAX_NETWORK_TESTS") != "1")
        {
            Assert.Skip("Set MINKQUICKLAX_NETWORK_TESTS=1 to run tests that reach the internet.");
        }
        using var fetcher = new FaviconFetcher();

        var bytes = await fetcher.FetchAsync(new Uri(url), TestContext.Current.CancellationToken);

        Assert.NotNull(bytes);
        Assert.True(FaviconFetcher.LooksLikeImage(bytes));
    }

    [Fact]
    public async Task NonWebAddresses_AreNotFetched()
    {
        using var fetcher = new FaviconFetcher();

        Assert.Null(await fetcher.FetchAsync(new Uri("file:///C:/Windows/win.ini"), TestContext.Current.CancellationToken));
    }
}
