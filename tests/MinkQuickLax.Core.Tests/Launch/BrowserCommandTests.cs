using MinkQuickLax.Core.Launch;

namespace MinkQuickLax.Core.Tests.Launch;

public sealed class BrowserCommandTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe\"", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe")]
    [InlineData("\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\" -osint -url \"%1\"", "C:\\Program Files\\Mozilla Firefox\\firefox.exe")]
    [InlineData("C:\\Program Files\\Internet Explorer\\iexplore.exe", "C:\\Program Files\\Internet Explorer\\iexplore.exe")]
    [InlineData("C:\\Tools\\browser.EXE --new-window", "C:\\Tools\\browser.EXE")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    [InlineData("\"", null)]
    public void FindsTheProgramInARegisteredCommand(string? command, string? expected)
    {
        Assert.Equal(expected, BrowserCommand.ExecutablePath(command));
    }

    [Theory]
    [InlineData("https://github.com", "\"https://github.com/\"")]
    [InlineData("http://example.com/a b?q=\"x\"", "\"http://example.com/a%20b?q=%22x%22\"")]
    [InlineData("https://ไทย.example/หน้า", "\"https://ไทย.example/%E0%B8%AB%E0%B8%99%E0%B9%89%E0%B8%B2\"")]
    public void WebAddressesBecomeOneQuotedArgument(string url, string expected)
    {
        Assert.Equal(expected, BrowserCommand.UrlArgument(url));
    }

    [Theory]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("--disable-web-security")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public void AnythingElseIsRefused(string url)
    {
        Assert.Null(BrowserCommand.UrlArgument(url));
    }
}
