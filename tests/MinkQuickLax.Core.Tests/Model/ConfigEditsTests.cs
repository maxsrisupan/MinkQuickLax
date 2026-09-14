using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Model;

public sealed class ConfigEditsTests
{
    private static Link L(string id) => new() { Id = id, Name = id.ToUpperInvariant(), Kind = LinkKind.File, Target = id };

    private static AppConfig Sample() => new()
    {
        Links = [L("a"), L("b"), L("c")],
        Groups = [new Group { Id = "g", Name = "Work", LinkIds = ["a", "b"] }],
        Placements =
        [
            new Placement { Id = "pa1", RefId = "a" },
            new Placement { Id = "pa2", RefId = "a" },
            new Placement { Id = "pg", Type = PlacementType.Group, RefId = "g" },
        ],
    };

    [Fact]
    public void UsageCount_CountsIconsAndGroups()
    {
        var config = Sample();

        Assert.Equal(3, config.UsageCount("a"));
        Assert.Equal(1, config.UsageCount("b"));
        Assert.Equal(0, config.UsageCount("c"));
    }

    [Fact]
    public void RemoveLink_RemovesItEverywhere()
    {
        var config = Sample().RemoveLink("a");

        Assert.Equal(["b", "c"], config.Links.Select(l => l.Id));
        Assert.Equal(["b"], config.Groups[0].LinkIds);
        Assert.Equal(["pg"], config.Placements.Select(p => p.Id));
    }

    [Fact]
    public void UpdateLink_ChangesEveryPlaceAtOnce()
    {
        var config = Sample();

        var renamed = config.UpdateLink(config.FindLink("a")! with { Name = "Renamed" });

        Assert.Equal("Renamed", renamed.FindLink("a")!.Name);
        Assert.Equal("A", config.FindLink("a")!.Name); // the original is untouched
    }

    [Fact]
    public void RemovePlacement_KeepsTheLink()
    {
        var config = Sample().RemovePlacement("pa1");

        Assert.Equal(3, config.Links.Count);
        Assert.Equal(["pa2", "pg"], config.Placements.Select(p => p.Id));
    }

    [Fact]
    public void GroupCanOnlyBePlacedOnce()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Sample().AddPlacement(new Placement { Type = PlacementType.Group, RefId = "g" }));
    }

    [Fact]
    public void CreateGroup_AddsGroupAndItsPlacement()
    {
        var group = new Group { Id = "g2", Name = "New", LinkIds = ["b", "c"] };

        var config = Sample().CreateGroup(group, new Placement { Id = "pg2", X = 0.3, Y = 0.4 });

        Assert.Equal(["g", "g2"], config.Groups.Select(g => g.Id));
        var placement = config.FindPlacement("pg2")!;
        Assert.Equal(PlacementType.Group, placement.Type);
        Assert.Equal("g2", placement.RefId);
    }

    [Fact]
    public void AddLinkToGroup_InsertsOnceAtTheIndex()
    {
        var config = Sample().AddLinkToGroup("g", "c", 0).AddLinkToGroup("g", "c", 1);

        Assert.Equal(["a", "c", "b"], config.FindGroup("g")!.LinkIds);
    }

    [Fact]
    public void MoveLinkInGroup_Reorders()
    {
        var config = Sample().MoveLinkInGroup("g", "a", 5);

        Assert.Equal(["b", "a"], config.FindGroup("g")!.LinkIds);
    }

    [Fact]
    public void RemoveLinkFromGroup_KeepsTheLink()
    {
        var config = Sample().RemoveLinkFromGroup("g", "a");

        Assert.Equal(["b"], config.FindGroup("g")!.LinkIds);
        Assert.NotNull(config.FindLink("a"));
    }

    [Fact]
    public void RemoveGroup_KeepsItsLinks()
    {
        var config = Sample().RemoveGroup("g");

        Assert.Empty(config.Groups);
        Assert.Equal(3, config.Links.Count);
        Assert.Equal(["pa1", "pa2"], config.Placements.Select(p => p.Id));
    }

    [Fact]
    public void EditingMissingItems_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Sample().AddLinkToGroup("nope", "a"));
        Assert.Throws<InvalidOperationException>(() => Sample().AddLinkToGroup("g", "nope"));
        Assert.Throws<InvalidOperationException>(() => Sample().UpdateLink(L("nope")));
    }
}
