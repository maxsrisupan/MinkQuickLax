using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace MinkQuickLax.Services;

/// <summary>
/// Serves user-visible text from Resources/Strings.resx. Bindings read through the indexer,
/// so switching <see cref="Culture"/> refreshes every bound text without reopening windows.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IndexerChanged = new(Binding.IndexerName);

    private readonly ResourceManager _resources =
        new("MinkQuickLax.Resources.Strings", typeof(Localizer).Assembly);

    private Localizer()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Localizer Instance { get; } = new();

    /// <summary>Defaults to the Windows display language; unsupported languages fall back to English.</summary>
    public CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

    public string this[string key] => _resources.GetString(key, Culture) ?? key;

    public void SetCulture(CultureInfo culture)
    {
        Culture = culture;
        PropertyChanged?.Invoke(this, IndexerChanged);
    }

    /// <summary>Creates a one-way binding to the text for <paramref name="key"/>.</summary>
    public Binding Bind(string key) => new($"[{key}]") { Source = this, Mode = BindingMode.OneWay };
}
