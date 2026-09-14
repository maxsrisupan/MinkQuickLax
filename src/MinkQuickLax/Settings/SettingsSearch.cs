using System.Globalization;
using System.Windows;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Services;

namespace MinkQuickLax.Settings;

/// <summary>Marks the element a search result scrolls to: <c>settings:SearchAnchor.Id="IconSize"</c>.</summary>
public static class SearchAnchor
{
    public static readonly DependencyProperty IdProperty =
        DependencyProperty.RegisterAttached("Id", typeof(string), typeof(SearchAnchor), new PropertyMetadata(null));

    public static string? GetId(DependencyObject element) => (string?)element.GetValue(IdProperty);

    public static void SetId(DependencyObject element, string? value) => element.SetValue(IdProperty, value);
}

/// <param name="Text">What the suggestion list shows: the setting's name and its page.</param>
public sealed record SettingsSearchResult(SettingsPage Page, string? Anchor, string Text)
{
    /// <summary>What screen readers announce for the result row.</summary>
    public string Name => Text;
}

/// <summary>
/// Finds settings by name in Thai and English at once (SPEC 4.8), plus extra words kept under <c>Search_&lt;Anchor&gt;</c>.
/// </summary>
public static class SettingsSearch
{
    private static readonly CultureInfo[] Languages = [CultureInfo.GetCultureInfo("th-TH"), CultureInfo.GetCultureInfo("en-US")];

    private static readonly (SettingsPage Page, string? Anchor, string NameKey)[] Entries =
    [
        (SettingsPage.Links, null, "Page_Links"),
        (SettingsPage.Groups, null, "Page_Groups"),
        (SettingsPage.Appearance, "Style", "Settings_Style"),
        (SettingsPage.Appearance, "Theme", "Settings_Theme"),
        (SettingsPage.Appearance, "IconSize", "Settings_IconSize"),
        (SettingsPage.Appearance, "IdleOpacity", "Settings_IdleOpacity"),
        (SettingsPage.Appearance, "ProximityRadius", "Settings_ProximityRadius"),
        (SettingsPage.Appearance, "Magnify", "Settings_Magnify"),
        (SettingsPage.Appearance, "ShowLabels", "Settings_ShowLabels"),
        (SettingsPage.Appearance, "ReduceMotion", "Settings_ReduceMotion"),
        (SettingsPage.Appearance, "GlassTint", "Settings_GlassTint"),
        (SettingsPage.Appearance, "GlassStrength", "Settings_GlassStrength"),
        (SettingsPage.Appearance, "GlassBlur", "Settings_GlassBlur"),
        (SettingsPage.Visibility, "TrayClick", "Settings_TrayClick"),
        (SettingsPage.Behavior, "LaunchOn", "Settings_LaunchOn"),
        (SettingsPage.Behavior, "TooltipDelay", "Settings_TooltipDelay"),
        (SettingsPage.Position, "GridSize", "Settings_GridSize"),
        (SettingsPage.Position, "Snap", "Settings_Snap"),
        (SettingsPage.Position, "CtrlDrag", "Settings_CtrlDrag"),
        (SettingsPage.Position, "AllowOverTaskbar", "Settings_AllowOverTaskbar"),
        (SettingsPage.Position, "Tidy", "Tray_Tidy"),
        (SettingsPage.Position, "ResetPositions", "Settings_ResetPositions"),
        (SettingsPage.General, "StartWithWindows", "Settings_StartWithWindows"),
        (SettingsPage.General, "Language", "Settings_Language"),
        (SettingsPage.General, "CheckUpdates", "Settings_CheckUpdates"),
        (SettingsPage.General, "UpdateChannel", "Settings_UpdateChannel"),
        (SettingsPage.Data, "Backup", "Data_Backup"),
        (SettingsPage.Data, "Restore", "Data_Restore"),
        (SettingsPage.Data, "OpenFolder", "Data_OpenFolder"),
        (SettingsPage.Data, "ResetAll", "Data_ResetAll"),
        (SettingsPage.Data, "DeleteAll", "Data_DeleteAll"),
        (SettingsPage.About, "CopyInfo", "About_CopyInfo"),
        (SettingsPage.About, "Licenses", "About_Licenses"),
        (SettingsPage.About, "GitHub", "About_GitHub"),
    ];

    /// <summary>Every page and setting key, so a test can check each one exists in the resources.</summary>
    public static IEnumerable<string> ResourceKeys => Entries.Select(e => e.NameKey).Concat(Enum.GetNames<SettingsPage>().Select(p => "Page_" + p));

    public static IReadOnlyList<SettingsSearchResult> Find(string query, Localizer text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return
        [
            .. Entries
                .Select(entry => (entry, score: TextSearch.Score(
                    query,
                    Languages.Select(language => text.Get(entry.NameKey, language)),
                    entry.Anchor is null ? [] : Languages.Select(language => text.Get("Search_" + entry.Anchor, language)))))
                .Where(match => match.score > 0)
                .OrderByDescending(match => match.score)
                .Take(12)
                .Select(match => new SettingsSearchResult(
                    match.entry.Page,
                    match.entry.Anchor,
                    match.entry.Anchor is null ? text[match.entry.NameKey] : $"{text[match.entry.NameKey]} · {text["Page_" + match.entry.Page]}")),
        ];
    }
}
