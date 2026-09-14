using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinkQuickLax.Core.Launch;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Shell;
using MinkQuickLax.Services;

namespace MinkQuickLax.Scanner;

public enum ScanFilter
{
    All,
    Programs,
    Store,
    Desktop,
}

public enum ItemSource
{
    Program,
    Store,
    Desktop,
    Manual,
}

/// <summary>One row in the scanner: what will become a link, with a name the user can change first.</summary>
public sealed partial class ScanItem : ObservableObject
{
    /// <summary>For web addresses: the browser chosen when it was added, or null for the default.</summary>
    public string? Browser { get; set; }

    public ScanItem(ItemSource source, LinkKind kind, string target, string arguments, string name, string? iconParsingName, bool alreadyAdded)
    {
        Source = source;
        Kind = kind;
        Target = target;
        Arguments = arguments;
        _name = name;
        IconParsingName = iconParsingName;
        AlreadyAdded = alreadyAdded;
        _icon = IconCache.LetterImage(name);
    }

    public ItemSource Source { get; }
    public LinkKind Kind { get; }
    public string Target { get; }
    public string Arguments { get; }
    public string? IconParsingName { get; }
    public bool AlreadyAdded { get; }
    public bool CanCheck => !AlreadyAdded;
    public string SourceLabel => Localizer.Instance[Source switch
    {
        ItemSource.Store => "Scanner_SourceStore",
        ItemSource.Desktop => "Scanner_SourceDesktop",
        ItemSource.Manual => "Scanner_SourceManual",
        _ => "Scanner_SourceProgram",
    }];

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private ImageSource _icon;

    public static string KeyOf(LinkKind kind, string target, string arguments) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind}|{target.ToUpperInvariant()}|{arguments}");
}

public sealed partial class ScannerViewModel : ObservableObject, IDisposable
{
    private readonly LinkAdder _adder;
    private readonly Localizer _text;
    private readonly HashSet<string> _existing;
    private readonly CancellationTokenSource _cancel = new();

    public ScannerViewModel(LinkAdder adder, Localizer text, IEnumerable<Link> existingLinks, bool firstRun)
    {
        _adder = adder;
        _text = text;
        FirstRun = firstRun;
        _existing = existingLinks.Select(l => ScanItem.KeyOf(l.Kind, l.Target, l.Arguments)).ToHashSet(StringComparer.Ordinal);
        View = CollectionViewSource.GetDefaultView(Items);
        View.Filter = Matches;
        Items.CollectionChanged += (_, _) => UpdateCounts();
        _status = text["Scanner_Scanning"];
    }

    public event Action? CloseRequested;

    public ObservableCollection<ScanItem> Items { get; } = [];

    public ICollectionView View { get; }

    public bool FirstRun { get; }

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private ScanFilter _filter;

    [ObservableProperty]
    private bool _isScanning = true;

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private string _selectionText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private int _selectedCount;

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _urlError = "";

    /// <summary>Browsers a web address can open with; the first is the Windows default (SPEC 4.1).</summary>
    public IReadOnlyList<BrowserChoice> Browsers { get; private set; } = [];

    [ObservableProperty]
    private BrowserChoice? _browser;

    public async Task ScanAsync()
    {
        Browsers = BrowserChoice.Load(_text);
        OnPropertyChanged(nameof(Browsers));
        Browser = Browsers[0];
        var apps = await Task.Run(() => AppScanner.Scan(_cancel.Token));
        foreach (var app in apps)
        {
            var source = app.Source switch { ScanSource.Store => ItemSource.Store, ScanSource.Desktop => ItemSource.Desktop, _ => ItemSource.Program };
            AddItem(new ScanItem(source, app.Kind, app.Target, app.Arguments, app.Name, app.IconParsingName,
                _existing.Contains(ScanItem.KeyOf(app.Kind, app.Target, app.Arguments))), atTop: false);
        }
        IsScanning = false;
        Status = string.Format(_text.Culture, _text["Scanner_Found"], Items.Count);
        await LoadIconsAsync(Items.ToList());
    }

    public void Dispose()
    {
        _cancel.Cancel();
        _cancel.Dispose();
    }

    partial void OnQueryChanged(string value) => View.Refresh();

    partial void OnFilterChanged(ScanFilter value) => View.Refresh();

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var chosen = Items.Where(i => i.IsChecked).ToList();
        var newLinks = await Task.Run(() => chosen.Select(item => new NewLink(
            new Link
            {
                Name = string.IsNullOrWhiteSpace(item.Name) ? item.Target : item.Name.Trim(),
                Kind = item.Kind,
                Target = item.Target,
                Arguments = item.Arguments,
                Icon = new IconSpec { Source = item.Kind == LinkKind.Url ? IconSourceKind.Favicon : IconSourceKind.Auto },
                Browser = item.Kind == LinkKind.Url ? item.Browser : null,
            },
            item.IconParsingName is null ? null : IconExtractor.ForParsingName(item.IconParsingName))).ToList());
        _adder.Add(newLinks);
        CloseRequested?.Invoke();
    }

    private bool CanAdd() => SelectedCount > 0;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    /// <summary>Adds a file, folder or web address typed or picked by the user, already ticked.</summary>
    public void AddManual(string input, string? browser = null)
    {
        var detected = LinkKindDetector.Detect(input, SystemPathProbe.Instance);
        if (detected is null)
        {
            return;
        }
        var iconName = detected.Kind is LinkKind.App or LinkKind.File or LinkKind.Folder or LinkKind.Command ? Environment.ExpandEnvironmentVariables(detected.Target) : null;
        var key = ScanItem.KeyOf(detected.Kind, detected.Target, "");
        var item = new ScanItem(ItemSource.Manual, detected.Kind, detected.Target, "", detected.SuggestedName, iconName, _existing.Contains(key));
        item.IsChecked = !item.AlreadyAdded;
        item.Browser = browser;
        AddItem(item, atTop: true);
        Query = "";
        Filter = ScanFilter.All;
        _ = LoadIconsAsync([item]);
    }

    [RelayCommand]
    private void AddUrl()
    {
        var detected = LinkKindDetector.Detect(Url, SystemPathProbe.Instance);
        if (detected is null || detected.Kind is not (LinkKind.Url or LinkKind.MsSettings))
        {
            UrlError = _text["Scanner_UrlInvalid"];
            return;
        }
        UrlError = "";
        AddManual(Url, Browser?.Id);
        Url = "";
    }

    private void AddItem(ScanItem item, bool atTop)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScanItem.IsChecked))
            {
                UpdateCounts();
            }
        };
        if (atTop)
        {
            Items.Insert(0, item);
        }
        else
        {
            Items.Add(item);
        }
    }

    private void UpdateCounts()
    {
        SelectedCount = Items.Count(i => i.IsChecked);
        SelectionText = SelectedCount == 0 ? _text["Scanner_NoneSelected"] : string.Format(_text.Culture, _text["Scanner_Selected"], SelectedCount);
    }

    private bool Matches(object obj)
    {
        if (obj is not ScanItem item)
        {
            return false;
        }
        var filterOk = Filter switch
        {
            ScanFilter.Programs => item.Source == ItemSource.Program,
            ScanFilter.Store => item.Source == ItemSource.Store,
            ScanFilter.Desktop => item.Source == ItemSource.Desktop,
            _ => true,
        };
        return filterOk && (Query.Length == 0 || item.Name.Contains(Query, StringComparison.CurrentCultureIgnoreCase));
    }

    private async Task LoadIconsAsync(IReadOnlyList<ScanItem> items)
    {
        using var gate = new SemaphoreSlim(4);
        var scale = 2; // 32 px rows look sharp up to 200%.
        var tasks = items.Where(i => i.IconParsingName is not null).Select(async item =>
        {
            await gate.WaitAsync(_cancel.Token);
            try
            {
                var bitmap = await Task.Run(() => IconExtractor.ForParsingName(item.IconParsingName!, 32 * scale), _cancel.Token);
                if (bitmap is not null)
                {
                    var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.Pixels, bitmap.Width * 4);
                    source.Freeze();
                    item.Icon = source;
                }
            }
            finally
            {
                gate.Release();
            }
        });
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Window closed while icons were loading.
        }
    }
}
