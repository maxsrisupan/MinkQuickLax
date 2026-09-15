using MinkQuickLax.Settings;

namespace MinkQuickLax.Manual;

/// <summary>
/// Section ids of the manual that the app opens directly (SPEC 4.9). ManualBuilder reads the <c>const string</c> values in
/// this file and fails the build when a chapter no longer has one of them.
/// </summary>
public static class ManualTopics
{
    public const string GettingStarted = "getting-started";
    public const string MissingTarget = "missing-target";
    public const string SettingsLinks = "settings-links";
    public const string SettingsGroups = "settings-groups";
    public const string SettingsAppearance = "settings-appearance";
    public const string SettingsVisibility = "settings-visibility";
    public const string SettingsBehavior = "settings-behavior";
    public const string SettingsPosition = "settings-position";
    public const string SettingsGeneral = "settings-general";
    public const string SettingsData = "settings-data";
    public const string SettingsAbout = "settings-about";

    /// <summary>F1 in the settings window opens the section about the page being shown.</summary>
    public static string ForPage(SettingsPage page) => page switch
    {
        SettingsPage.Links => SettingsLinks,
        SettingsPage.Groups => SettingsGroups,
        SettingsPage.Appearance => SettingsAppearance,
        SettingsPage.Visibility => SettingsVisibility,
        SettingsPage.Behavior => SettingsBehavior,
        SettingsPage.Position => SettingsPosition,
        SettingsPage.General => SettingsGeneral,
        SettingsPage.Data => SettingsData,
        _ => SettingsAbout,
    };
}
