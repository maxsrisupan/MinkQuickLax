using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MinkQuickLax.Platform.Shell;

namespace MinkQuickLax.Services;

/// <summary>One entry of a "open with" list for web links (SPEC 4.1). <see cref="Id"/> null is the Windows default browser.</summary>
public sealed partial class BrowserChoice(string? id, string name, string? exePath) : ObservableObject
{
    private const int IconSize = 32;

    public string? Id { get; } = id;

    public string Name { get; } = name;

    [ObservableProperty]
    private ImageSource? _icon;

    /// <summary>The default browser first, then every installed browser; icons load in the background.</summary>
    public static IReadOnlyList<BrowserChoice> Load(Localizer text)
    {
        var list = new List<BrowserChoice> { new(null, text["Browser_Default"], null) };
        list.AddRange(BrowserCatalog.List().Select(b => new BrowserChoice(b.Id, b.Name, b.ExePath)));
        foreach (var choice in list.Where(c => c.Id is not null))
        {
            _ = choice.LoadIconAsync(exePath: choice._exePath!);
        }
        return list;
    }

    /// <summary>The entry for a stored browser id, or null when that browser is no longer installed.</summary>
    public static BrowserChoice? Find(IReadOnlyList<BrowserChoice> choices, string? id) =>
        choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    private readonly string? _exePath = exePath;

    private async Task LoadIconAsync(string exePath)
    {
        var bitmap = await Task.Run(() => IconExtractor.ForParsingName(exePath, IconSize));
        if (bitmap is null)
        {
            return;
        }
        var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.Pixels, bitmap.Width * 4);
        source.Freeze();
        Icon = source;
    }
}
