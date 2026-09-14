using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Shell;

namespace MinkQuickLax.Services;

public sealed record NewLink(Link Link, IconBitmap? Icon);

/// <summary>Adds links and places each one in the next free spot from the top-right corner of the primary monitor (SPEC 4.12).</summary>
public sealed partial class LinkAdder(ConfigStore store, PlacementController placements, IconCache icons, ILogger<LinkAdder> logger)
{
    public void Add(IReadOnlyList<NewLink> items)
    {
        if (items.Count == 0)
        {
            return;
        }
        foreach (var item in items.Where(i => i.Icon is not null))
        {
            icons.Seed(item.Link, item.Icon!);
        }

        var settings = store.Current.Settings;
        var primary = PositionMapper.Primary(placements.Monitors);
        var area = PositionMapper.PlacementArea(primary, settings.AllowOverTaskbar);
        var cell = PositionMapper.WindowSize(settings.IconSize, primary);
        var occupied = placements.Windows.Where(w => w.IsVisible).Select(w => w.SquareRect).ToList();
        var slots = TidyLayout.FreeSlots(items.Count, cell, area, primary.ToPixels(settings.GridSize), occupied);

        var newPlacements = items
            .Select((item, i) => PositionMapper.WithCenter(new Placement { Type = PlacementType.Link, RefId = item.Link.Id }, slots[i], primary, settings.AllowOverTaskbar))
            .ToList();
        placements.PopInNext(newPlacements.Select(p => p.Id));
        store.Update(c =>
        {
            var next = c.AddLinks(items.Select(i => i.Link));
            foreach (var placement in newPlacements)
            {
                next = next.AddPlacement(placement);
            }
            return next;
        });
        LogAdded(logger, items.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Added {Count} links")]
    private static partial void LogAdded(ILogger logger, int count);
}
