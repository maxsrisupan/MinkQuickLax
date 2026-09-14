using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Model;

public sealed class LinkSummaryTests
{
    [Theory]
    [InlineData(LinkKind.App, @"C:\Program Files\Google\Chrome\Application\chrome.exe", "chrome.exe")]
    [InlineData(LinkKind.File, @"D:\งาน\รายงาน.docx", "รายงาน.docx")]
    [InlineData(LinkKind.Folder, @"C:\Users\me\Downloads\", "Downloads")]
    [InlineData(LinkKind.Folder, @"C:\", @"C:\")]
    [InlineData(LinkKind.Url, "https://github.com/maxsrisupan/MinkQuickLax", "github.com")]
    [InlineData(LinkKind.Url, "not a url", "not a url")]
    [InlineData(LinkKind.MsSettings, "ms-settings:display", "display")]
    [InlineData(LinkKind.Command, "%windir%", "%windir%")]
    [InlineData(LinkKind.ShellApp, "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "")]
    public void DescribesTheTarget(LinkKind kind, string target, string expected)
    {
        Assert.Equal(expected, LinkSummary.Subject(new Link { Kind = kind, Target = target }));
    }
}
