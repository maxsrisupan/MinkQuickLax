namespace MinkQuickLax.Core.Model;

/// <summary>The settings window's sections (SPEC 4.8); each one can go back to its defaults on its own.</summary>
public enum SettingsSection
{
    Appearance,
    Visibility,
    Behavior,
    Position,
    Hotkeys,
    General,
}

public static class SettingsSections
{
    private static readonly AppSettings Defaults = new();

    /// <summary>Puts every setting of <paramref name="section"/> back to its default and keeps the rest.</summary>
    public static AppSettings Reset(this AppSettings settings, SettingsSection section) => section switch
    {
        SettingsSection.Appearance => settings with
        {
            Style = Defaults.Style,
            Theme = Defaults.Theme,
            IconSize = Defaults.IconSize,
            IdleOpacity = Defaults.IdleOpacity,
            ProximityRadius = Defaults.ProximityRadius,
            Magnify = Defaults.Magnify,
            ShowLabels = Defaults.ShowLabels,
            ReduceMotion = Defaults.ReduceMotion,
            Glass = new GlassSettings { Extra = settings.Glass.Extra },
        },
        SettingsSection.Visibility => settings with
        {
            TrayClickToggles = Defaults.TrayClickToggles,
            HideOnFullscreen = Defaults.HideOnFullscreen,
        },
        SettingsSection.Behavior => settings with
        {
            LaunchOn = Defaults.LaunchOn,
            TooltipDelayMs = Defaults.TooltipDelayMs,
            GroupOpenOn = Defaults.GroupOpenOn,
        },
        SettingsSection.Position => settings with
        {
            GridSize = Defaults.GridSize,
            Snap = Defaults.Snap,
            CtrlDragMove = Defaults.CtrlDragMove,
            AllowOverTaskbar = Defaults.AllowOverTaskbar,
        },
        SettingsSection.Hotkeys => settings with { Hotkeys = new HotkeySettings { Extra = settings.Hotkeys.Extra } },
        SettingsSection.General => settings with
        {
            Language = Defaults.Language,
            CheckForUpdates = Defaults.CheckForUpdates,
            UpdateChannel = Defaults.UpdateChannel,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };
}
