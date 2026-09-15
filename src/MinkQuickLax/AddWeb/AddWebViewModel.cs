using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinkQuickLax.Core.Launch;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Services;

namespace MinkQuickLax.AddWeb;

/// <summary>"Add website" (SPEC 4.1): one address with its name and browser, put on screen in a single step.</summary>
public sealed partial class AddWebViewModel : ObservableObject, IDisposable
{
    /// <summary>How long typing has to pause before the site's icon is fetched, so half-typed hosts are not asked.</summary>
    private static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(600);

    private readonly LinkAdder _adder;
    private readonly IconCache _icons;
    private readonly Localizer _text;
    private readonly HashSet<string> _existing;
    private CancellationTokenSource? _preview;
    private ImageSource? _favicon;

    /// <summary>The name taken from the address; the name box follows it until the user types a name of their own.</summary>
    private string _suggestedName = "";

    public AddWebViewModel(LinkAdder adder, IconCache icons, Localizer text, IEnumerable<Link> existingLinks)
    {
        _adder = adder;
        _icons = icons;
        _text = text;
        _existing = existingLinks.Where(l => l.Kind is LinkKind.Url or LinkKind.MsSettings).Select(l => l.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Browsers = BrowserChoice.Load(text);
        _browser = Browsers[0];
    }

    public event Action? CloseRequested;

    /// <summary>Browsers the address can open with; the first is the Windows default.</summary>
    public IReadOnlyList<BrowserChoice> Browsers { get; }

    [ObservableProperty]
    private BrowserChoice? _browser;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _address = "";

    [ObservableProperty]
    private string _name = "";

    /// <summary>The site's icon, or a letter icon of the name; null while there is no name yet.</summary>
    [ObservableProperty]
    private ImageSource? _icon;

    [ObservableProperty]
    private string _error = "";

    [ObservableProperty]
    private bool _alreadyAdded;

    public void Dispose() => CancelPreview();

    partial void OnAddressChanged(string value)
    {
        Error = "";
        var parsed = WebAddress.Parse(value);
        var suggested = parsed?.SuggestedName ?? "";
        if (Name.Length == 0 || Name == _suggestedName)
        {
            Name = suggested;
        }
        _suggestedName = suggested;
        AlreadyAdded = parsed is not null && _existing.Contains(parsed.Target);

        CancelPreview();
        _favicon = null;
        UpdateIcon();
        if (parsed is not null && WebAddress.IsHttp(parsed.Target))
        {
            _preview = new CancellationTokenSource();
            _ = PreviewAsync(parsed.Target, _preview.Token);
        }
    }

    partial void OnNameChanged(string value) => UpdateIcon();

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        var parsed = WebAddress.Parse(Address);
        if (parsed is null)
        {
            Error = _text["AddWeb_Invalid"];
            return;
        }
        _adder.Add([new NewLink(WebLink(parsed, string.IsNullOrWhiteSpace(Name) ? parsed.SuggestedName : Name.Trim(), Browser?.Id), null)]);
        CloseRequested?.Invoke();
    }

    private bool CanAdd() => !string.IsNullOrWhiteSpace(Address);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    /// <summary>The link to store. The preview asks the icon cache for the same target, so the new icon shows its picture at once.</summary>
    private static Link WebLink(DetectedLink parsed, string name, string? browser)
    {
        var web = parsed.Kind == LinkKind.Url;
        return new Link
        {
            Name = name,
            Kind = parsed.Kind,
            Target = parsed.Target,
            Icon = new IconSpec { Source = web ? IconSourceKind.Favicon : IconSourceKind.Auto },
            Browser = web ? browser : null,
        };
    }

    private async Task PreviewAsync(string target, CancellationToken cancel)
    {
        try
        {
            await Task.Delay(PreviewDelay, cancel);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        var favicon = await _icons.GetAsync(WebLink(new DetectedLink(LinkKind.Url, target, ""), "", null));
        if (!cancel.IsCancellationRequested)
        {
            _favicon = favicon;
            UpdateIcon();
        }
    }

    private void UpdateIcon()
    {
        var name = string.IsNullOrWhiteSpace(Name) ? _suggestedName : Name;
        Icon = _favicon ?? (name.Length == 0 ? null : IconCache.LetterImage(name));
    }

    private void CancelPreview()
    {
        _preview?.Cancel();
        _preview?.Dispose();
        _preview = null;
    }
}
