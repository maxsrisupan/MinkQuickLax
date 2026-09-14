using System.Text.Json;
using System.Text.Json.Serialization;

namespace MinkQuickLax.Core.Model;

/// <summary>
/// The whole config.json document. Treat every instance as immutable and change it with <c>with</c> or
/// <see cref="ConfigEdits"/>; the setters exist because the JSON source generator overwrites
/// property defaults with null/0 when init-only properties are missing from the file.
/// </summary>
public sealed record AppConfig
{
    public int SchemaVersion { get; set; } = Config.ConfigMigrator.CurrentVersion;
    public IReadOnlyList<Link> Links { get; set; } = [];
    public IReadOnlyList<Group> Groups { get; set; } = [];
    public IReadOnlyList<Placement> Placements { get; set; } = [];
    public AppSettings Settings { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public static AppConfig CreateDefault() => new();
}

/// <summary>Something that can be opened: app, file, folder, URL…</summary>
public sealed record Link
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "";
    public LinkKind Kind { get; set; }
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool RunAsAdmin { get; set; }
    public IconSpec Icon { get; set; } = new();

    /// <summary>For web links: the browser to open with, as its StartMenuInternet key name. Null means the Windows default browser.</summary>
    public string? Browser { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record IconSpec
{
    public IconSourceKind Source { get; set; }

    /// <summary>Image file for <see cref="IconSourceKind.File"/>.</summary>
    public string? Path { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record Group
{
    public string Id { get; set; } = Ids.New();
    public string Name { get; set; } = "";
    public GroupDisplay Display { get; set; }
    public int Columns { get; set; } = 4;
    public bool ShowLabels { get; set; } = true;
    public IReadOnlyList<string> LinkIds { get; set; } = [];
    public IconSpec Icon { get; set; } = new() { Source = IconSourceKind.Preview };

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A single icon or a group placed on screen.</summary>
public sealed record Placement
{
    public string Id { get; set; } = Ids.New();
    public PlacementType Type { get; set; }
    public string RefId { get; set; } = "";

    /// <summary>Monitor device path from QueryDisplayConfig.</summary>
    public string Monitor { get; set; } = "";

    /// <summary>EDID manufacturer-product-serial, used when the device path changes (another port).</summary>
    public string? MonitorEdid { get; set; }

    /// <summary>Icon center as a 0–1 fraction of the monitor's work area.</summary>
    public double X { get; set; }

    public double Y { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record AppSettings
{
    public LanguageSetting Language { get; set; }
    public StyleSetting Style { get; set; }
    public ThemeSetting Theme { get; set; }
    public int IconSize { get; set; } = 48;
    public double IdleOpacity { get; set; } = 0.7;
    public int ProximityRadius { get; set; } = 130;
    public bool Magnify { get; set; } = true;
    public bool ShowLabels { get; set; }
    public ReduceMotionSetting ReduceMotion { get; set; }
    public LaunchTrigger LaunchOn { get; set; }
    public int TooltipDelayMs { get; set; } = 350;
    public GroupOpenTrigger GroupOpenOn { get; set; }
    public int GridSize { get; set; } = 24;
    public SnapMode Snap { get; set; }
    public bool CtrlDragMove { get; set; } = true;
    public bool AllowOverTaskbar { get; set; }
    public bool TrayClickToggles { get; set; } = true;
    public bool HideOnFullscreen { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public UpdateChannel UpdateChannel { get; set; }
    public HotkeySettings Hotkeys { get; set; } = new();
    public GlassSettings Glass { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record HotkeySettings
{
    public string? QuickSearch { get; set; } = "Win+Alt+Q";
    public string? ToggleVisibility { get; set; }
    public bool Enabled { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record GlassSettings
{
    public GlassTint Tint { get; set; }
    public double TintStrength { get; set; } = 0.18;
    public bool Blur { get; set; } = true;
    public bool Specular { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Allowed ranges for numeric settings; the settings UI uses the same limits.</summary>
public static class SettingsLimits
{
    public const int IconSizeMin = 32;
    public const int IconSizeMax = 64;
    public const double IdleOpacityMin = 0.3;
    public const double IdleOpacityMax = 1.0;
    public const int ProximityRadiusMin = 40;
    public const int ProximityRadiusMax = 400;
    public const int TooltipDelayMinMs = 0;
    public const int TooltipDelayMaxMs = 2000;
    public const int GridSizeMin = 8;
    public const int GridSizeMax = 96;
    public const double TintStrengthMin = 0;
    public const double TintStrengthMax = 0.7;
    public const int GroupColumnsMin = 1;
    public const int GroupColumnsMax = 12;
}

public static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N");
}
