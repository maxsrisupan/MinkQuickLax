namespace ManualBuilder.Tests;

public sealed class ManualProjectTests
{
    private const string Chapter = """
        # เริ่มต้นใช้งาน {#getting-started}

        เปิด app แล้ว **ไอคอน** ขึ้นที่มุมขวาบน ดู[คีย์ลัด](#hotkeys)

        ## คีย์ลัด {#hotkeys}

        | คีย์ | ทำอะไร |
        |---|---|
        | `Win+Alt+Q` | หน้าค้นหาด่วน |

        - ข้อแรก
        - ข้อสอง
        """;

    [Fact]
    public void AValidManual_HasNoErrors_AndEnglishFallsBackToThai()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", Chapter);

        var project = manual.Load();

        Assert.False(project.HasErrors, string.Join('\n', project.Diagnostics));
        var english = Assert.Single(project.Chapters["en"]);
        Assert.True(english.IsFallback);
        Assert.Contains(project.Diagnostics, d => d is { Severity: Severity.Note, Code: "MANUAL101" });
    }

    [Fact]
    public void HeadingsWithoutIds_AreErrors()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", "# เริ่มต้น {#start}\n\n## ไม่มี id\n");

        var error = Assert.Single(manual.Load().Diagnostics, d => d.Severity == Severity.Error);

        Assert.Equal("MANUAL004", error.Code);
        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void RepeatedIds_AreErrors_AcrossChapters()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-a.md", "# ก {#alpha}\n\n## ซ้ำ {#same}\n");
        manual.Write("th/02-b.md", "# ข {#beta}\n\n## ซ้ำ {#same}\n");

        var error = Assert.Single(manual.Load().Diagnostics, d => d.Severity == Severity.Error);

        Assert.Equal("MANUAL006", error.Code);
        Assert.Equal("th/02-b.md", error.File);
    }

    [Theory]
    [InlineData("Getting-Started")]
    [InlineData("คีย์ลัด")]
    [InlineData("two--dashes")]
    public void IdsMustBeLowercaseEnglishWords(string id)
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-a.md", $"# ก {{#{id}}}\n");

        Assert.Contains(manual.Load().Diagnostics, d => d.Code == "MANUAL005");
    }

    [Fact]
    public void LinksToMissingSections_AreErrors()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-a.md", "# ก {#alpha}\n\nดู [ที่นี่](#nowhere)\n");

        var error = Assert.Single(manual.Load().Diagnostics, d => d.Severity == Severity.Error);

        Assert.Equal("MANUAL008", error.Code);
        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void KeywordsForUnknownSections_AreErrors()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", Chapter);
        manual.Write("keywords.json", """{ "hotkeys": ["hotkey", "shortcut"], "gone": ["x"] }""");

        var project = manual.Load();

        Assert.Equal("MANUAL010", Assert.Single(project.Diagnostics, d => d.Severity == Severity.Error).Code);
        Assert.Equal(["hotkey", "shortcut"], project.Keywords["hotkeys"]);
    }

    [Fact]
    public void TopicsTheAppOpens_MustExist()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", Chapter);
        var topics = manual.Write("ManualTopics.cs", """
            public static class ManualTopics
            {
                public const string Start = "getting-started";
                public const string Missing = "settings-icon-size";
            }
            """);

        var error = Assert.Single(manual.Load(topics).Diagnostics, d => d.Severity == Severity.Error);

        Assert.Equal("MANUAL012", error.Code);
        Assert.Equal(4, error.Line);
    }

    [Fact]
    public void Translations_KeepTheThaiIds()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", Chapter);
        manual.Write("en/01-start.md", "# Getting started {#getting-started}\n\n## Extra {#extra}\n");

        var project = manual.Load();

        Assert.Contains(project.Diagnostics, d => d is { Severity: Severity.Error, Code: "MANUAL007" });
        Assert.Contains(project.Diagnostics, d => d is { Severity: Severity.Note, Code: "MANUAL102" } && d.Message.Contains("hotkeys", StringComparison.Ordinal));
        Assert.False(Assert.Single(project.Chapters["en"]).IsFallback);
    }

    [Fact]
    public void EnglishChaptersWithoutThai_AreErrors()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", Chapter);
        manual.Write("en/02-extra.md", "# Extra {#extra}\n");

        Assert.Contains(manual.Load().Diagnostics, d => d is { Code: "MANUAL002", File: "en/02-extra.md" });
    }

    [Fact]
    public void Sections_SplitAtHeadings_AndKeepTableAndListText()
    {
        using var manual = new ManualFixture();
        manual.Write("th/01-start.md", Chapter);

        var sections = manual.Load().Chapters["th"][0].Sections().ToList();

        Assert.Equal(["getting-started", "hotkeys"], sections.Select(s => s.Id));
        Assert.Contains("ไอคอน ขึ้นที่มุมขวาบน", sections[0].Text, StringComparison.Ordinal);
        Assert.Contains("Win+Alt+Q", sections[1].Text, StringComparison.Ordinal);
        Assert.Contains("ข้อสอง", sections[1].Text, StringComparison.Ordinal);
        Assert.Equal("เริ่มต้นใช้งาน", sections[1].Chapter);
    }
}
