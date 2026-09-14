using MinkQuickLax.Core.Launch;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Launch;

public sealed class LinkKindDetectorTests
{
    private sealed class FakeProbe : IPathProbe
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool FileExists(string path) => Files.Contains(path);

        public bool DirectoryExists(string path) => Folders.Contains(path);
    }

    private readonly FakeProbe _probe = new()
    {
        Files = { @"C:\Tools\app.exe", @"C:\Docs\report.pdf", @"C:\Scripts\build.ps1", @"C:\Users\me\Desktop\Code.lnk" },
        Folders = { @"D:\งาน", @"C:\Tools" },
    };

    [Theory]
    [InlineData("https://github.com/maxsrisupan", LinkKind.Url, "https://github.com/maxsrisupan", "github.com")]
    [InlineData("http://www.example.org", LinkKind.Url, "http://www.example.org", "example.org")]
    [InlineData("www.example.org/path", LinkKind.Url, "https://www.example.org/path", "example.org")]
    [InlineData("mailto:me@example.com", LinkKind.Url, "mailto:me@example.com", "mailto")]
    [InlineData("ms-settings:display", LinkKind.MsSettings, "ms-settings:display", "display")]
    [InlineData("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", LinkKind.ShellApp, "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "WindowsCalculator")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", LinkKind.ShellApp, "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "WindowsTerminal")]
    [InlineData(@"D:\งาน", LinkKind.Folder, @"D:\งาน", "งาน")]
    [InlineData(@"C:\Tools\app.exe", LinkKind.App, @"C:\Tools\app.exe", "app")]
    [InlineData(@"""C:\Tools\app.exe""", LinkKind.App, @"C:\Tools\app.exe", "app")]
    [InlineData(@"C:\Users\me\Desktop\Code.lnk", LinkKind.App, @"C:\Users\me\Desktop\Code.lnk", "Code")]
    [InlineData(@"C:\Scripts\build.ps1", LinkKind.Command, @"C:\Scripts\build.ps1", "build")]
    [InlineData(@"C:\Docs\report.pdf", LinkKind.File, @"C:\Docs\report.pdf", "report")]
    [InlineData(@"E:\Offline\notes.txt", LinkKind.File, @"E:\Offline\notes.txt", "notes")]
    [InlineData(@"E:\Offline\Projects\", LinkKind.Folder, @"E:\Offline\Projects\", "Projects")]
    [InlineData(@"E:", LinkKind.Folder, @"E:", "E:")]
    [InlineData(@"\\server\share\file.docx", LinkKind.File, @"\\server\share\file.docx", "file")]
    public void Detects(string input, LinkKind kind, string target, string name)
    {
        var detected = LinkKindDetector.Detect(input, _probe);

        Assert.NotNull(detected);
        Assert.Equal(kind, detected.Kind);
        Assert.Equal(target, detected.Target);
        Assert.Equal(name, detected.SuggestedName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData(@"shell:AppsFolder\")]
    public void Nothing_ReturnsNull(string? input)
    {
        Assert.Null(LinkKindDetector.Detect(input, _probe));
    }

    [Fact]
    public void DriveLetterPath_IsNotMistakenForAUrlScheme()
    {
        Assert.Equal(LinkKind.App, LinkKindDetector.Detect(@"c:\tools\APP.EXE", _probe)!.Kind);
    }
}
