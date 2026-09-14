using System.Text.Json.Serialization;
using MinkQuickLax.Core.Config;

namespace MinkQuickLax.Core.Model;

// Every enum is stored as a camelCase string. Value 0 is what an unreadable value falls back to.

[JsonConverter(typeof(TolerantEnumConverter<LinkKind>))]
public enum LinkKind
{
    /// <summary>A kind this version does not know (for example written by a newer version).</summary>
    Unknown,
    App,
    ShellApp,
    File,
    Folder,
    Url,
    Command,
    MsSettings,
}

[JsonConverter(typeof(TolerantEnumConverter<IconSourceKind>))]
public enum IconSourceKind
{
    Auto,
    File,
    Favicon,
    Letter,
    Preview,
}

[JsonConverter(typeof(TolerantEnumConverter<GroupDisplay>))]
public enum GroupDisplay
{
    Folder,
    Bar,
}

[JsonConverter(typeof(TolerantEnumConverter<PlacementType>))]
public enum PlacementType
{
    Link,
    Group,
}

[JsonConverter(typeof(TolerantEnumConverter<LanguageSetting>))]
public enum LanguageSetting
{
    System,
    Th,
    En,
}

[JsonConverter(typeof(TolerantEnumConverter<StyleSetting>))]
public enum StyleSetting
{
    Glass,
    Hud,
    Dot,
}

[JsonConverter(typeof(TolerantEnumConverter<ThemeSetting>))]
public enum ThemeSetting
{
    System,
    Light,
    Dark,
}

[JsonConverter(typeof(TolerantEnumConverter<ReduceMotionSetting>))]
public enum ReduceMotionSetting
{
    System,
    On,
    Off,
}

[JsonConverter(typeof(TolerantEnumConverter<LaunchTrigger>))]
public enum LaunchTrigger
{
    SingleClick,
    DoubleClick,
}

[JsonConverter(typeof(TolerantEnumConverter<GroupOpenTrigger>))]
public enum GroupOpenTrigger
{
    Click,
    Hover,
}

[JsonConverter(typeof(TolerantEnumConverter<SnapMode>))]
public enum SnapMode
{
    GridAndAlign,
    Grid,
    None,
}

[JsonConverter(typeof(TolerantEnumConverter<UpdateChannel>))]
public enum UpdateChannel
{
    Stable,
    Beta,
}

[JsonConverter(typeof(TolerantEnumConverter<GlassTint>))]
public enum GlassTint
{
    Auto,
    Lagoon,
    Ember,
    Ink,
}
