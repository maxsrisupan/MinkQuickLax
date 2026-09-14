using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Model;

public sealed class SettingsSectionsTests
{
    private static readonly AppSettings Defaults = new();

    // Every setting differs from its default, so a reset that misses one shows up.
    private static AppSettings AllChanged() => new()
    {
        Language = LanguageSetting.Th,
        Style = StyleSetting.Hud,
        Theme = ThemeSetting.Dark,
        IconSize = 60,
        IdleOpacity = 0.5,
        ProximityRadius = 200,
        Magnify = false,
        ShowLabels = true,
        ReduceMotion = ReduceMotionSetting.On,
        LaunchOn = LaunchTrigger.DoubleClick,
        TooltipDelayMs = 900,
        GroupOpenOn = GroupOpenTrigger.Hover,
        GridSize = 32,
        Snap = SnapMode.None,
        CtrlDragMove = false,
        AllowOverTaskbar = true,
        TrayClickToggles = false,
        HideOnFullscreen = false,
        CheckForUpdates = false,
        UpdateChannel = UpdateChannel.Beta,
        Hotkeys = new HotkeySettings { QuickSearch = null, ToggleVisibility = "Win+Alt+H", Enabled = false },
        Glass = new GlassSettings { Tint = GlassTint.Ember, TintStrength = 0.4, Blur = false, Specular = false },
    };

    [Fact]
    public void TheSampleChangesEverySetting()
    {
        var changed = AllChanged();
        foreach (var property in typeof(AppSettings).GetProperties().Where(p => p.Name != nameof(AppSettings.Extra)))
        {
            Assert.False(Equals(property.GetValue(changed), property.GetValue(Defaults)), $"{property.Name} is not changed by the sample; add it.");
        }
    }

    [Fact]
    public void EverySettingBelongsToASection()
    {
        var settings = AllChanged();
        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            settings = settings.Reset(section);
        }

        Assert.Equal(Defaults, settings);
    }

    [Fact]
    public void ResettingOneSection_KeepsTheOthers()
    {
        var reset = AllChanged().Reset(SettingsSection.Position);

        Assert.Equal(Defaults.GridSize, reset.GridSize);
        Assert.Equal(Defaults.AllowOverTaskbar, reset.AllowOverTaskbar);
        Assert.Equal(60, reset.IconSize);
        Assert.Equal(LanguageSetting.Th, reset.Language);
        Assert.Equal(GlassTint.Ember, reset.Glass.Tint);
    }
}
