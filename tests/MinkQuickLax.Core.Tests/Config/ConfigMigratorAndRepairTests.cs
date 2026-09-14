using System.Text.Json.Nodes;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Config;

public sealed class ConfigMigratorTests
{
    [Fact]
    public void FileWithoutVersion_IsMigratedFromZero()
    {
        var result = ConfigSerializer.Deserialize("""{ "settings": { "opacity": 55 } }""");

        Assert.Equal(0, result.FileSchemaVersion);
        Assert.True(result.WasMigrated);
        Assert.Equal(ConfigMigrator.CurrentVersion, result.Config.SchemaVersion);
        Assert.Equal(0.55, result.Config.Settings.IdleOpacity, 3);
        Assert.DoesNotContain("opacity", ConfigSerializer.Serialize(result.Config).Replace("idleOpacity", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroToOne_KeepsExistingIdleOpacity()
    {
        var root = JsonNode.Parse("""{ "settings": { "opacity": 55, "idleOpacity": 0.9 } }""")!.AsObject();

        ConfigMigrator.Migrate(root);

        Assert.Equal(0.9, root["settings"]!["idleOpacity"]!.GetValue<double>());
        Assert.Null(root["settings"]!["opacity"]);
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public void CurrentVersion_IsLeftAlone()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 1, "settings": { "opacity": 55 } }""")!.AsObject();

        Assert.Equal(1, ConfigMigrator.Migrate(root));
        Assert.NotNull(root["settings"]!["opacity"]);
    }

    [Fact]
    public void NewerVersion_IsReportedAndNotDowngraded()
    {
        var result = ConfigSerializer.Deserialize("""{ "schemaVersion": 7, "links": [] }""");

        Assert.True(result.IsNewerThanApp);
        Assert.Equal(7, result.Config.SchemaVersion);
    }

    [Fact]
    public void VersionWrittenAsString_IsUnderstood()
    {
        Assert.Equal(1, ConfigSerializer.Deserialize("""{ "schemaVersion": "1" }""").FileSchemaVersion);
    }
}

public sealed class ConfigRepairTests
{
    private static Link L(string id) => new() { Id = id, Name = id, Kind = LinkKind.File, Target = id };

    [Fact]
    public void ValidConfig_HasNoIssues()
    {
        var config = new AppConfig
        {
            Links = [L("a"), L("b")],
            Groups = [new Group { Id = "g", LinkIds = ["a", "b"] }],
            Placements = [new Placement { Id = "p1", RefId = "a", X = 0.5, Y = 0.5 }, new Placement { Id = "p2", Type = PlacementType.Group, RefId = "g" }],
        };

        var (_, issues) = ConfigRepair.Repair(config);

        Assert.Empty(issues);
    }

    [Fact]
    public void DanglingReferences_AreRemoved()
    {
        var config = new AppConfig
        {
            Links = [L("a")],
            Groups = [new Group { Id = "g", LinkIds = ["a", "missing", "a"] }],
            Placements =
            [
                new Placement { Id = "p1", RefId = "missing" },
                new Placement { Id = "p2", Type = PlacementType.Group, RefId = "nope" },
                new Placement { Id = "p3", RefId = "a" },
            ],
        };

        var (fixedConfig, issues) = ConfigRepair.Repair(config);

        Assert.Equal(["a"], fixedConfig.Groups[0].LinkIds);
        Assert.Equal(["p3"], fixedConfig.Placements.Select(p => p.Id));
        Assert.Equal(4, issues.Count);
    }

    [Fact]
    public void GroupPlacedTwice_KeepsTheFirst()
    {
        var config = new AppConfig
        {
            Groups = [new Group { Id = "g" }],
            Placements = [new Placement { Id = "p1", Type = PlacementType.Group, RefId = "g" }, new Placement { Id = "p2", Type = PlacementType.Group, RefId = "g" }],
        };

        var (fixedConfig, _) = ConfigRepair.Repair(config);

        Assert.Equal(["p1"], fixedConfig.Placements.Select(p => p.Id));
    }

    [Fact]
    public void DuplicateIds_KeepTheFirst()
    {
        var config = new AppConfig { Links = [L("a"), L("a") with { Name = "second" }, L("b")] };

        var (fixedConfig, _) = ConfigRepair.Repair(config);

        Assert.Equal(["a", "b"], fixedConfig.Links.Select(l => l.Id));
        Assert.Equal("a", fixedConfig.Links[0].Name);
    }

    [Fact]
    public void OutOfRangeValues_AreClamped()
    {
        var config = new AppConfig
        {
            Links = [L("a")],
            Groups = [new Group { Id = "g", Columns = 0 }],
            Placements = [new Placement { Id = "p", RefId = "a", X = 1.7, Y = double.NaN }],
            Settings = new AppSettings { IconSize = 500, IdleOpacity = -1, GridSize = 2, Glass = new GlassSettings { TintStrength = double.PositiveInfinity } },
        };

        var (fixedConfig, issues) = ConfigRepair.Repair(config);

        Assert.Equal(SettingsLimits.GroupColumnsMin, fixedConfig.Groups[0].Columns);
        Assert.Equal((1.0, 0.5), (fixedConfig.Placements[0].X, fixedConfig.Placements[0].Y));
        Assert.Equal(SettingsLimits.IconSizeMax, fixedConfig.Settings.IconSize);
        Assert.Equal(SettingsLimits.IdleOpacityMin, fixedConfig.Settings.IdleOpacity);
        Assert.Equal(SettingsLimits.GridSizeMin, fixedConfig.Settings.GridSize);
        Assert.Equal(0.18, fixedConfig.Settings.Glass.TintStrength);
        Assert.Equal(6, issues.Count);
    }

    [Fact]
    public void NullsFromJson_BecomeEmpty()
    {
        const string Json = """
            { "schemaVersion": 1, "links": null, "groups": [ { "id": "g", "linkIds": null, "name": null } ],
              "placements": [ null ], "settings": { "hotkeys": null, "glass": null } }
            """;

        var (config, issues) = ConfigRepair.Repair(ConfigSerializer.Deserialize(Json).Config);

        Assert.Empty(config.Links);
        Assert.Empty(config.Groups[0].LinkIds);
        Assert.Equal("", config.Groups[0].Name);
        Assert.Empty(config.Placements);
        Assert.NotNull(config.Settings.Hotkeys);
        Assert.NotNull(config.Settings.Glass);
        Assert.Single(issues);
    }
}
