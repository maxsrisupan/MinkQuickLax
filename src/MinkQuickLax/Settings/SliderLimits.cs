using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Settings;

/// <summary><see cref="SettingsLimits"/> as doubles: XAML cannot put an int constant into Slider.Minimum or Maximum.</summary>
public static class SliderLimits
{
    public static double IconSizeMin => SettingsLimits.IconSizeMin;

    public static double IconSizeMax => SettingsLimits.IconSizeMax;

    public static double ProximityRadiusMin => SettingsLimits.ProximityRadiusMin;

    public static double ProximityRadiusMax => SettingsLimits.ProximityRadiusMax;

    public static double TooltipDelayMin => SettingsLimits.TooltipDelayMinMs;

    public static double TooltipDelayMax => SettingsLimits.TooltipDelayMaxMs;

    public static double GridSizeMin => SettingsLimits.GridSizeMin;

    public static double GridSizeMax => SettingsLimits.GridSizeMax;

    public static double GroupColumnsMin => SettingsLimits.GroupColumnsMin;

    public static double GroupColumnsMax => SettingsLimits.GroupColumnsMax;
}
