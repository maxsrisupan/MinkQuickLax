namespace MinkQuickLax.Core.Model;

/// <summary>Pure edits on <see cref="AppConfig"/> that keep links, groups and placements consistent.</summary>
public static class ConfigEdits
{
    public static Link? FindLink(this AppConfig config, string id) => config.Links.FirstOrDefault(l => l.Id == id);

    public static Group? FindGroup(this AppConfig config, string id) => config.Groups.FirstOrDefault(g => g.Id == id);

    public static Placement? FindPlacement(this AppConfig config, string id) => config.Placements.FirstOrDefault(p => p.Id == id);

    public static AppConfig AddLinks(this AppConfig config, IEnumerable<Link> links) =>
        config with { Links = [.. config.Links, .. links] };

    public static AppConfig UpdateLink(this AppConfig config, Link link) =>
        config with { Links = Replace(config.Links, l => l.Id == link.Id, link) };

    /// <summary>How many places show this link: single icons plus groups that contain it.</summary>
    public static int UsageCount(this AppConfig config, string linkId) =>
        config.Placements.Count(p => p.Type == PlacementType.Link && p.RefId == linkId)
        + config.Groups.Count(g => g.LinkIds.Contains(linkId));

    /// <summary>Deletes a link everywhere: its single icons and its entries in groups.</summary>
    public static AppConfig RemoveLink(this AppConfig config, string linkId) =>
        config with
        {
            Links = [.. config.Links.Where(l => l.Id != linkId)],
            Groups = [.. config.Groups.Select(g => g.LinkIds.Contains(linkId) ? g with { LinkIds = [.. g.LinkIds.Where(id => id != linkId)] } : g)],
            Placements = [.. config.Placements.Where(p => !(p.Type == PlacementType.Link && p.RefId == linkId))],
        };

    public static AppConfig AddPlacement(this AppConfig config, Placement placement)
    {
        if (placement.Type == PlacementType.Group && config.Placements.Any(p => p.Type == PlacementType.Group && p.RefId == placement.RefId))
        {
            throw new InvalidOperationException($"Group {placement.RefId} is already placed.");
        }
        return config with { Placements = [.. config.Placements, placement] };
    }

    public static AppConfig UpdatePlacement(this AppConfig config, Placement placement) =>
        config with { Placements = Replace(config.Placements, p => p.Id == placement.Id, placement) };

    /// <summary>"Remove from screen": the link or group itself stays.</summary>
    public static AppConfig RemovePlacement(this AppConfig config, string placementId) =>
        config with { Placements = [.. config.Placements.Where(p => p.Id != placementId)] };

    /// <summary>Creates a group from links and places it where <paramref name="placement"/> says.</summary>
    public static AppConfig CreateGroup(this AppConfig config, Group group, Placement placement) =>
        (config with { Groups = [.. config.Groups, group] }).AddPlacement(placement with { Type = PlacementType.Group, RefId = group.Id });

    public static AppConfig UpdateGroup(this AppConfig config, Group group) =>
        config with { Groups = Replace(config.Groups, g => g.Id == group.Id, group) };

    /// <summary>Deletes a group and its placement; the links inside are kept.</summary>
    public static AppConfig RemoveGroup(this AppConfig config, string groupId) =>
        config with
        {
            Groups = [.. config.Groups.Where(g => g.Id != groupId)],
            Placements = [.. config.Placements.Where(p => !(p.Type == PlacementType.Group && p.RefId == groupId))],
        };

    /// <summary>Adds a link to a group at <paramref name="index"/> (end when null). A link appears in a group at most once.</summary>
    public static AppConfig AddLinkToGroup(this AppConfig config, string groupId, string linkId, int? index = null)
    {
        var group = config.FindGroup(groupId) ?? throw new InvalidOperationException($"Group {groupId} does not exist.");
        if (config.FindLink(linkId) is null)
        {
            throw new InvalidOperationException($"Link {linkId} does not exist.");
        }
        var ids = group.LinkIds.Where(id => id != linkId).ToList();
        ids.Insert(Math.Clamp(index ?? ids.Count, 0, ids.Count), linkId);
        return config.UpdateGroup(group with { LinkIds = ids });
    }

    public static AppConfig RemoveLinkFromGroup(this AppConfig config, string groupId, string linkId)
    {
        var group = config.FindGroup(groupId) ?? throw new InvalidOperationException($"Group {groupId} does not exist.");
        return config.UpdateGroup(group with { LinkIds = [.. group.LinkIds.Where(id => id != linkId)] });
    }

    public static AppConfig MoveLinkInGroup(this AppConfig config, string groupId, string linkId, int newIndex) =>
        config.AddLinkToGroup(groupId, linkId, newIndex);

    private static List<T> Replace<T>(IReadOnlyList<T> items, Func<T, bool> match, T replacement)
    {
        var list = items.ToList();
        var index = list.FindIndex(i => match(i));
        if (index < 0)
        {
            throw new InvalidOperationException("Item to update does not exist.");
        }
        list[index] = replacement;
        return list;
    }
}
