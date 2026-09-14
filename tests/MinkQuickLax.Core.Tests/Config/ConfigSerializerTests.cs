using System.Text.Json;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Config;

public sealed class ConfigSerializerTests
{
    // The example from PLAN.md 4.2, comments included.
    private const string PlanExample = """
        {
          "schemaVersion": 1,
          "links": [
            {
              "id": "0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e",   // GUID N format
              "name": "Visual Studio Code",
              "kind": "app",
              "target": "C:\\Users\\me\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe",
              "arguments": "",
              "workingDirectory": "",
              "runAsAdmin": false,
              "icon": { "source": "auto" }
            },
            { "id": "l2", "name": "GitHub", "kind": "url", "target": "https://github.com", "icon": { "source": "favicon" } }
          ],
          "groups": [
            { "id": "g1", "name": "งาน", "display": "folder", "columns": 4, "showLabels": true,
              "linkIds": ["0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e", "l2"], "icon": { "source": "preview" } }
          ],
          "placements": [
            { "id": "p1", "type": "link", "refId": "0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e", "monitor": "\\\\?\\DISPLAY#DELA1B2#x", "x": 0.92, "y": 0.08 }
          ],
          "settings": {
            "language": "system", "style": "glass", "theme": "system", "iconSize": 48, "idleOpacity": 0.7,
            "proximityRadius": 130, "magnify": true, "showLabels": false, "reduceMotion": "system",
            "launchOn": "singleClick", "tooltipDelayMs": 350, "groupOpenOn": "click", "gridSize": 24,
            "snap": "gridAndAlign", "ctrlDragMove": true, "allowOverTaskbar": false, "trayClickToggles": true,
            "hideOnFullscreen": true, "checkForUpdates": true, "updateChannel": "stable",
            "hotkeys": { "quickSearch": "Win+Alt+Q", "toggleVisibility": null, "enabled": true },
            "glass": { "tint": "auto", "tintStrength": 0.18, "blur": true, "specular": true }
          }
        }
        """;

    [Fact]
    public void PlanExample_Deserializes()
    {
        var result = ConfigSerializer.Deserialize(PlanExample);
        var config = result.Config;

        Assert.Equal(1, result.FileSchemaVersion);
        Assert.False(result.WasMigrated);
        Assert.Equal(2, config.Links.Count);
        Assert.Equal(LinkKind.App, config.Links[0].Kind);
        Assert.Equal(IconSourceKind.Favicon, config.Links[1].Icon.Source);
        Assert.Equal("งาน", config.Groups[0].Name);
        Assert.Equal(["0b9f3c1e8a2d4f6b9c7e5a3d1f2b4c6e", "l2"], config.Groups[0].LinkIds);
        Assert.Equal(0.92, config.Placements[0].X);
        Assert.Equal(SnapMode.GridAndAlign, config.Settings.Snap);
        Assert.Equal("Win+Alt+Q", config.Settings.Hotkeys.QuickSearch);
        Assert.Null(config.Settings.Hotkeys.ToggleVisibility);
    }

    [Fact]
    public void Default_RoundTrips()
    {
        var json = ConfigSerializer.Serialize(AppConfig.CreateDefault());
        var back = ConfigSerializer.Deserialize(json).Config;

        Assert.Equal(json, ConfigSerializer.Serialize(back));
        Assert.Equal(new AppSettings() with { Hotkeys = back.Settings.Hotkeys, Glass = back.Settings.Glass }, back.Settings);
        Assert.Equal(new HotkeySettings(), back.Settings.Hotkeys);
        Assert.Equal(new GlassSettings(), back.Settings.Glass);
    }

    [Fact]
    public void Enums_AreWrittenAsCamelCaseStrings()
    {
        var config = AppConfig.CreateDefault() with
        {
            Links = [new Link { Id = "a", Kind = LinkKind.ShellApp, Target = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" }],
            Settings = new AppSettings { LaunchOn = LaunchTrigger.DoubleClick, UpdateChannel = UpdateChannel.Beta },
        };

        var json = ConfigSerializer.Serialize(config);

        Assert.Contains("\"kind\": \"shellApp\"", json, StringComparison.Ordinal);
        Assert.Contains("\"launchOn\": \"doubleClick\"", json, StringComparison.Ordinal);
        Assert.Contains("\"snap\": \"gridAndAlign\"", json, StringComparison.Ordinal);
        Assert.Contains("\"updateChannel\": \"beta\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ThaiAndSymbols_StayReadableInTheFile()
    {
        var config = AppConfig.CreateDefault() with { Links = [new Link { Id = "a", Name = "ที่ทำงาน & บ้าน", Kind = LinkKind.Folder, Target = @"D:\งาน" }] };

        var json = ConfigSerializer.Serialize(config);

        Assert.Contains("\"name\": \"ที่ทำงาน & บ้าน\"", json, StringComparison.Ordinal);
        Assert.Contains("\"quickSearch\": \"Win+Alt+Q\"", json, StringComparison.Ordinal);
        Assert.Equal("ที่ทำงาน & บ้าน", ConfigSerializer.Deserialize(json).Config.Links[0].Name);
    }

    [Fact]
    public void UnknownFields_ArePreservedAtEveryLevel()
    {
        const string Json = """
            {
              "schemaVersion": 1,
              "futureTopLevel": { "a": 1 },
              "links": [ { "id": "a", "name": "A", "kind": "file", "target": "x", "futureLinkField": [1, 2] } ],
              "settings": { "futureSetting": true, "glass": { "futureGlass": "shiny" } }
            }
            """;

        var json = ConfigSerializer.Serialize(ConfigSerializer.Deserialize(Json).Config);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("futureTopLevel").GetProperty("a").GetInt32());
        Assert.Equal(2, root.GetProperty("links")[0].GetProperty("futureLinkField").GetArrayLength());
        Assert.True(root.GetProperty("settings").GetProperty("futureSetting").GetBoolean());
        Assert.Equal("shiny", root.GetProperty("settings").GetProperty("glass").GetProperty("futureGlass").GetString());
    }

    [Fact]
    public void UnknownEnumValues_FallBackInsteadOfFailing()
    {
        const string Json = """
            { "schemaVersion": 1,
              "links": [ { "id": "a", "kind": "hologram", "target": "x" } ],
              "settings": { "style": "neon", "snap": 7, "theme": { "odd": true } } }
            """;

        var config = ConfigSerializer.Deserialize(Json).Config;

        Assert.Equal(LinkKind.Unknown, config.Links[0].Kind);
        Assert.Equal(StyleSetting.Glass, config.Settings.Style);
        Assert.Equal(SnapMode.GridAndAlign, config.Settings.Snap);
        Assert.Equal(ThemeSetting.System, config.Settings.Theme);
    }

    [Fact]
    public void MissingFields_UseDefaults()
    {
        var config = ConfigSerializer.Deserialize("""{ "schemaVersion": 1, "settings": { "iconSize": 56 } }""").Config;

        Assert.Empty(config.Links);
        Assert.Equal(56, config.Settings.IconSize);
        Assert.Equal(0.7, config.Settings.IdleOpacity);
        Assert.True(config.Settings.CtrlDragMove);
        Assert.Equal(0.18, config.Settings.Glass.TintStrength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"links\": [ ")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{ \"links\": \"oops\" }")]
    public void Garbage_ThrowsConfigFormatException(string text)
    {
        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(text));
    }
}
