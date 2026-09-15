using MinkQuickLax.Core.Launch;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Launch;

public sealed class WebAddressTests
{
    [Theory]
    [InlineData("youtube.com", LinkKind.Url, "https://youtube.com", "youtube.com")]
    [InlineData("  https://github.com/maxsrisupan  ", LinkKind.Url, "https://github.com/maxsrisupan", "github.com")]
    [InlineData("\"https://example.org\"", LinkKind.Url, "https://example.org", "example.org")]
    [InlineData("http://www.example.org", LinkKind.Url, "http://www.example.org", "example.org")]
    [InlineData("www.example.org/path?q=1", LinkKind.Url, "https://www.example.org/path?q=1", "example.org")]
    [InlineData("example.com:8080/admin", LinkKind.Url, "https://example.com:8080/admin", "example.com")]
    [InlineData("mail.google.com/mail/u/0/#inbox", LinkKind.Url, "https://mail.google.com/mail/u/0/#inbox", "mail.google.com")]
    [InlineData("localhost:3000", LinkKind.Url, "http://localhost:3000", "localhost")]
    [InlineData("192.168.1.1", LinkKind.Url, "http://192.168.1.1", "192.168.1.1")]
    [InlineData("mailto:me@example.com", LinkKind.Url, "mailto:me@example.com", "mailto")]
    [InlineData("ms-settings:display", LinkKind.MsSettings, "ms-settings:display", "display")]
    public void Parse_AcceptsAddresses(string input, LinkKind kind, string target, string name)
    {
        var link = WebAddress.Parse(input);

        Assert.NotNull(link);
        Assert.Equal(kind, link.Kind);
        Assert.Equal(target, link.Target);
        Assert.Equal(name, link.SuggestedName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("https//youtube.com")]
    [InlineData("https://")]
    [InlineData("https:")]
    [InlineData(".com")]
    [InlineData("example..com")]
    [InlineData(@"C:\Tools\app.exe")]
    [InlineData(@"\\server\share")]
    public void Parse_RejectsWhatIsNotAnAddress(string? input)
    {
        Assert.Null(WebAddress.Parse(input));
    }

    [Theory]
    [InlineData("https://example.org", true)]
    [InlineData("http://localhost:3000", true)]
    [InlineData("mailto:me@example.com", false)]
    [InlineData("ms-settings:display", false)]
    [InlineData("youtube.com", false)]
    public void IsHttp_OnlyForWebPages(string target, bool expected)
    {
        Assert.Equal(expected, WebAddress.IsHttp(target));
    }
}
