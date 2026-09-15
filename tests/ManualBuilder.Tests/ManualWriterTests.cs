using System.Text;

namespace ManualBuilder.Tests;

public sealed class ManualWriterTests
{
    [Fact]
    public void WritesOneSelfContainedFilePerLanguage()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", "# เริ่มต้น {#start}\n\n![หน้าจอ](../assets/shot.png)\n\n## คีย์ลัด {#hotkeys}\n\nกด `Win+Alt+Q`\n");
        manual.Write("keywords.json", """{ "hotkeys": ["hotkey"] }""");
        Directory.CreateDirectory(Path.Combine(manual.Directory, "assets"));
        File.WriteAllBytes(Path.Combine(manual.Directory, "assets", "shot.png"), [0x89, 0x50, 0x4E, 0x47]);
        var output = Path.Combine(manual.Directory, "out");
        var project = manual.Load();
        Assert.False(project.HasErrors, string.Join('\n', project.Diagnostics));

        var files = new ManualWriter(project, manual.FontsDirectory).Write(output);

        Assert.Equal(["th.html", "en.html"], files.Select(Path.GetFileName));
        var thai = File.ReadAllText(files[0], Encoding.UTF8);
        Assert.Contains("<html lang=\"th\">", thai, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"hotkeys\">", thai, StringComparison.Ordinal);
        Assert.Contains("href=\"#hotkeys\"", thai, StringComparison.Ordinal);
        Assert.Contains("src=\"data:image/png;base64,iVBORw==\"", thai, StringComparison.Ordinal);
        Assert.Contains("@font-face{font-family:\"IBM Plex Sans Thai\"", thai, StringComparison.Ordinal);
        Assert.Contains("\"keywords\":[\"hotkey\"]", thai, StringComparison.Ordinal);
        Assert.Contains("<a href=\"en.html\">English</a>", thai, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", thai, StringComparison.Ordinal);

        var english = File.ReadAllText(files[1], Encoding.UTF8);
        Assert.Contains("<p class=\"untranslated\">Not translated yet</p>", english, StringComparison.Ordinal);
        Assert.Contains("<h2 id=\"hotkeys\">", english, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesUnchangedFilesAlone()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", "# เริ่มต้น {#start}\n");
        var output = Path.Combine(manual.Directory, "out");
        var writer = new ManualWriter(manual.Load(), manual.FontsDirectory);
        var first = writer.Write(output)[0];
        var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first, stamp);

        new ManualWriter(manual.Load(), manual.FontsDirectory).Write(output);

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(first));
    }
}
