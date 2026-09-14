using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Services;

namespace MinkQuickLax.Settings;

/// <summary>
/// Appearance, hide/show, usage, position and general settings. Every change goes straight into the config, so it
/// takes effect at once (SPEC 4.8); changes made elsewhere flow back through <see cref="ConfigStore.Changed"/>.
/// </summary>
public sealed partial class PreferencesViewModel : ObservableObject, IDisposable
{
    private readonly ConfigStore _store;
    private readonly StartupRegistration _startup;
    private readonly PlacementController _placements;
    private readonly SettingsDialogs _dialogs;
    private readonly Localizer _text;

    public PreferencesViewModel(ConfigStore store, StartupRegistration startup, PlacementController placements, SettingsDialogs dialogs, Localizer text)
    {
        _store = store;
        _startup = startup;
        _placements = placements;
        _dialogs = dialogs;
        _text = text;
        _store.Changed += OnConfigChanged;
        _text.PropertyChanged += OnLanguageChanged;
    }

    private AppSettings Current => _store.Current.Settings;

    // Appearance
    public StyleSetting Style { get => Current.Style; set => Set(s => s with { Style = value }); }

    public string StyleNote => _text[Current.Style switch
    {
        StyleSetting.Hud => "Style_HudNote",
        StyleSetting.Dot => "Style_DotNote",
        _ => "Style_GlassNote",
    }];

    public ThemeSetting Theme { get => Current.Theme; set => Set(s => s with { Theme = value }); }

    public double IconSize { get => Current.IconSize; set => Set(s => s with { IconSize = Clamp((int)Math.Round(value), SettingsLimits.IconSizeMin, SettingsLimits.IconSizeMax) }); }

    public string IconSizeText => _text.Format("Unit_Pixels", Current.IconSize);

    public double IdleOpacityPercent
    {
        get => Math.Round(Current.IdleOpacity * 100);
        set => Set(s => s with { IdleOpacity = Math.Clamp(Math.Round(value) / 100, SettingsLimits.IdleOpacityMin, SettingsLimits.IdleOpacityMax) });
    }

    public string IdleOpacityText => _text.Format("Unit_Percent", IdleOpacityPercent);

    public double ProximityRadius
    {
        get => Current.ProximityRadius;
        set => Set(s => s with { ProximityRadius = Clamp((int)Math.Round(value), SettingsLimits.ProximityRadiusMin, SettingsLimits.ProximityRadiusMax) });
    }

    public string ProximityRadiusText => _text.Format("Unit_Pixels", Current.ProximityRadius);

    public bool Magnify { get => Current.Magnify; set => Set(s => s with { Magnify = value }); }

    public bool ShowLabels { get => Current.ShowLabels; set => Set(s => s with { ShowLabels = value }); }

    public ReduceMotionSetting ReduceMotion { get => Current.ReduceMotion; set => Set(s => s with { ReduceMotion = value }); }

    public GlassTint GlassTint { get => Current.Glass.Tint; set => Set(s => s with { Glass = s.Glass with { Tint = value } }); }

    public double GlassStrengthPercent
    {
        get => Math.Round(Current.Glass.TintStrength * 100);
        set => Set(s => s with { Glass = s.Glass with { TintStrength = Math.Clamp(Math.Round(value) / 100, SettingsLimits.TintStrengthMin, SettingsLimits.TintStrengthMax) } });
    }

    public string GlassStrengthText => _text.Format("Unit_Percent", GlassStrengthPercent);

    public bool GlassBlur { get => Current.Glass.Blur; set => Set(s => s with { Glass = s.Glass with { Blur = value } }); }

    // Hide and show
    public bool TrayClickToggles { get => Current.TrayClickToggles; set => Set(s => s with { TrayClickToggles = value }); }

    // Using icons
    public LaunchTrigger LaunchOn { get => Current.LaunchOn; set => Set(s => s with { LaunchOn = value }); }

    public double TooltipDelay
    {
        get => Current.TooltipDelayMs;
        set => Set(s => s with { TooltipDelayMs = Clamp((int)Math.Round(value / 50) * 50, SettingsLimits.TooltipDelayMinMs, SettingsLimits.TooltipDelayMaxMs) });
    }

    public string TooltipDelayText => _text.Format("Unit_Milliseconds", Current.TooltipDelayMs);

    // Position
    public double GridSize { get => Current.GridSize; set => Set(s => s with { GridSize = Clamp((int)Math.Round(value), SettingsLimits.GridSizeMin, SettingsLimits.GridSizeMax) }); }

    public string GridSizeText => _text.Format("Unit_Pixels", Current.GridSize);

    public SnapMode Snap { get => Current.Snap; set => Set(s => s with { Snap = value }); }

    public bool CtrlDragMove { get => Current.CtrlDragMove; set => Set(s => s with { CtrlDragMove = value }); }

    public bool AllowOverTaskbar { get => Current.AllowOverTaskbar; set => Set(s => s with { AllowOverTaskbar = value }); }

    // General: the startup state lives in the registry, never in the config (SPEC 4.10).
    public bool StartWithWindows
    {
        get => _startup.Read() == StartupState.On;
        set
        {
            if (value)
            {
                _startup.Enable(AppInfo.ExePath);
            }
            else
            {
                _startup.Disable();
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartupDisabledInTaskManager));
        }
    }

    public bool StartupDisabledInTaskManager => _startup.Read() == StartupState.DisabledInTaskManager;

    public LanguageSetting Language { get => Current.Language; set => Set(s => s with { Language = value }); }

    public bool CheckForUpdates { get => Current.CheckForUpdates; set => Set(s => s with { CheckForUpdates = value }); }

    public UpdateChannel UpdateChannel { get => Current.UpdateChannel; set => Set(s => s with { UpdateChannel = value }); }

    public void Dispose()
    {
        _store.Changed -= OnConfigChanged;
        _text.PropertyChanged -= OnLanguageChanged;
    }

    /// <summary>Re-reads values that live outside the config (the startup registration) when a page is shown.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(StartupDisabledInTaskManager));
    }

    [RelayCommand]
    private async Task ResetAsync(SettingsSection section)
    {
        if (await _dialogs.ConfirmAsync(_text["Settings_Reset"], _text["Settings_ResetConfirm"], _text["Settings_Reset"]))
        {
            Set(s => s.Reset(section));
        }
    }

    [RelayCommand]
    private void Tidy() => _placements.Tidy();

    [RelayCommand]
    private async Task ResetPositionsAsync()
    {
        if (await _dialogs.ConfirmAsync(_text["Settings_ResetPositions"].TrimEnd('…'), _text["Settings_ResetPositionsConfirm"], _text["Settings_Reset"]))
        {
            Set(s => s.Reset(SettingsSection.Position));
            _placements.Tidy();
        }
    }

    private void Set(Func<AppSettings, AppSettings> change) =>
        _store.Update(config =>
        {
            var next = change(config.Settings);
            return Equals(next, config.Settings) ? config : config with { Settings = next };
        });

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (!Equals(e.Previous.Settings, e.Current.Settings))
        {
            OnPropertyChanged(string.Empty);
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(string.Empty);

    private static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
}
