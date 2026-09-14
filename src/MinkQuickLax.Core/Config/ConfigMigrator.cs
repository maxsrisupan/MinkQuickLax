using System.Text.Json;
using System.Text.Json.Nodes;

namespace MinkQuickLax.Core.Config;

/// <summary>Upgrades the raw JSON of older config files one schema version at a time.</summary>
public static class ConfigMigrator
{
    public const int CurrentVersion = 1;

    // Steps[n] upgrades version n to n + 1.
    private static readonly Action<JsonObject>[] Steps =
    [
        From0To1,
    ];

    /// <summary>Migrates <paramref name="root"/> in place and returns the version the file had.</summary>
    public static int Migrate(JsonObject root)
    {
        var fileVersion = ReadVersion(root);
        for (var v = Math.Max(fileVersion, 0); v < CurrentVersion; v++)
        {
            Steps[v](root);
        }
        if (fileVersion < CurrentVersion)
        {
            root["schemaVersion"] = CurrentVersion;
        }
        return fileVersion;
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root["schemaVersion"] is JsonValue value)
        {
            if (value.TryGetValue(out int number))
            {
                return number;
            }
            if (value.TryGetValue(out string? text) && int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }
        return 0;
    }

    /// <summary>
    /// Example step. Version 0 is any file without "schemaVersion"; such a draft stored the idle
    /// opacity as a percentage in "settings.opacity" (0–100) instead of "settings.idleOpacity" (0–1).
    /// </summary>
    private static void From0To1(JsonObject root)
    {
        if (root["settings"] is not JsonObject settings || settings["opacity"] is not JsonValue opacity)
        {
            return;
        }
        settings.Remove("opacity");
        if (!settings.ContainsKey("idleOpacity") && opacity.GetValueKind() == JsonValueKind.Number)
        {
            settings["idleOpacity"] = opacity.GetValue<double>() / 100.0;
        }
    }
}
