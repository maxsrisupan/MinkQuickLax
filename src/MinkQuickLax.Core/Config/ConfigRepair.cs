using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Config;

/// <summary>Makes a loaded config internally consistent and reports what it had to fix.</summary>
public static class ConfigRepair
{
    public static (AppConfig Config, IReadOnlyList<string> Issues) Repair(AppConfig config)
    {
        var issues = new List<string>();

        var links = Dedupe(config.Links ?? [], l => l.Id, "link", issues)
            .Select(l => l with
            {
                Name = l.Name ?? "",
                Target = l.Target ?? "",
                Arguments = l.Arguments ?? "",
                WorkingDirectory = l.WorkingDirectory ?? "",
                Icon = l.Icon ?? new IconSpec(),
            })
            .ToList();
        var linkIds = links.Select(l => l.Id).ToHashSet(StringComparer.Ordinal);

        var groups = Dedupe(config.Groups ?? [], g => g.Id, "group", issues)
            .Select(g =>
            {
                var members = new List<string>();
                foreach (var id in g.LinkIds ?? [])
                {
                    if (id is null || !linkIds.Contains(id))
                    {
                        issues.Add($"group {g.Id}: removed missing link {id}");
                    }
                    else if (members.Contains(id, StringComparer.Ordinal))
                    {
                        issues.Add($"group {g.Id}: removed duplicate link {id}");
                    }
                    else
                    {
                        members.Add(id);
                    }
                }
                var columns = Math.Clamp(g.Columns, SettingsLimits.GroupColumnsMin, SettingsLimits.GroupColumnsMax);
                if (columns != g.Columns)
                {
                    issues.Add($"group {g.Id}: columns {g.Columns} -> {columns}");
                }
                return g with { Name = g.Name ?? "", LinkIds = members, Columns = columns, Icon = g.Icon ?? new IconSpec { Source = IconSourceKind.Preview } };
            })
            .ToList();
        var groupIds = groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);

        var placedGroups = new HashSet<string>(StringComparer.Ordinal);
        var placements = new List<Placement>();
        foreach (var p in Dedupe(config.Placements ?? [], p => p.Id, "placement", issues))
        {
            var exists = p.Type == PlacementType.Group ? groupIds.Contains(p.RefId ?? "") : linkIds.Contains(p.RefId ?? "");
            if (!exists)
            {
                issues.Add($"placement {p.Id}: removed, {p.Type} {p.RefId} does not exist");
                continue;
            }
            if (p.Type == PlacementType.Group && !placedGroups.Add(p.RefId!))
            {
                issues.Add($"placement {p.Id}: removed, group {p.RefId} is already placed");
                continue;
            }
            var x = Fraction(p.X);
            var y = Fraction(p.Y);
            if (x != p.X || y != p.Y)
            {
                issues.Add($"placement {p.Id}: position ({p.X},{p.Y}) -> ({x},{y})");
            }
            placements.Add(p with { X = x, Y = y, Monitor = p.Monitor ?? "" });
        }

        var settings = RepairSettings(config.Settings ?? new AppSettings(), issues);

        var repaired = config with { Links = links, Groups = groups, Placements = placements, Settings = settings };
        return (repaired, issues);
    }

    private static AppSettings RepairSettings(AppSettings s, List<string> issues)
    {
        int ClampInt(string name, int value, int min, int max)
        {
            var clamped = Math.Clamp(value, min, max);
            if (clamped != value)
            {
                issues.Add($"settings.{name}: {value} -> {clamped}");
            }
            return clamped;
        }

        double ClampDouble(string name, double value, double min, double max, double fallback)
        {
            var clamped = double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
            if (clamped != value)
            {
                issues.Add($"settings.{name}: {value} -> {clamped}");
            }
            return clamped;
        }

        var defaults = new AppSettings();
        var glass = s.Glass ?? new GlassSettings();
        return s with
        {
            IconSize = ClampInt("iconSize", s.IconSize, SettingsLimits.IconSizeMin, SettingsLimits.IconSizeMax),
            IdleOpacity = ClampDouble("idleOpacity", s.IdleOpacity, SettingsLimits.IdleOpacityMin, SettingsLimits.IdleOpacityMax, defaults.IdleOpacity),
            ProximityRadius = ClampInt("proximityRadius", s.ProximityRadius, SettingsLimits.ProximityRadiusMin, SettingsLimits.ProximityRadiusMax),
            TooltipDelayMs = ClampInt("tooltipDelayMs", s.TooltipDelayMs, SettingsLimits.TooltipDelayMinMs, SettingsLimits.TooltipDelayMaxMs),
            GridSize = ClampInt("gridSize", s.GridSize, SettingsLimits.GridSizeMin, SettingsLimits.GridSizeMax),
            Hotkeys = s.Hotkeys ?? new HotkeySettings(),
            Glass = glass with
            {
                TintStrength = ClampDouble("glass.tintStrength", glass.TintStrength, SettingsLimits.TintStrengthMin, SettingsLimits.TintStrengthMax, defaults.Glass.TintStrength),
            },
        };
    }

    private static double Fraction(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5;

    private static IEnumerable<T> Dedupe<T>(IEnumerable<T?> items, Func<T, string?> id, string what, List<string> issues)
        where T : class
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null)
            {
                issues.Add($"{what}: removed null entry");
                continue;
            }
            var key = id(item);
            if (string.IsNullOrWhiteSpace(key))
            {
                issues.Add($"{what}: removed entry without id");
                continue;
            }
            if (!seen.Add(key))
            {
                issues.Add($"{what} {key}: removed duplicate id");
                continue;
            }
            yield return item;
        }
    }
}
