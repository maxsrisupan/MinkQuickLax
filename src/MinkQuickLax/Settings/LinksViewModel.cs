using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Launch;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Shell;
using MinkQuickLax.Services;

namespace MinkQuickLax.Settings;

/// <summary>One row of the link list.</summary>
public sealed partial class LinkRow(string id) : ObservableObject
{
    public string Id { get; } = id;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _kindText = "";

    [ObservableProperty]
    private string _target = "";

    [ObservableProperty]
    private ImageSource? _icon;

    public LinkKind Kind { get; set; }

    public bool IsPlaced { get; set; }

    internal Link? Link { get; set; }
}

/// <summary>The Links page (SPEC 4.8): every link, searchable and filterable, with an editor for the chosen one.</summary>
public sealed partial class LinksViewModel : ObservableObject, IDisposable
{
    // Index 0 is "all kinds"; the rest follow the kinds F1 can add.
    private static readonly LinkKind[] FilterKinds = [LinkKind.App, LinkKind.ShellApp, LinkKind.File, LinkKind.Folder, LinkKind.Url];

    private readonly ConfigStore _store;
    private readonly IconCache _icons;
    private readonly LinkAdder _adder;
    private readonly Scanner.ScannerService _scanner;
    private readonly SettingsDialogs _dialogs;
    private readonly Localizer _text;
    private readonly ObservableCollection<LinkRow> _rows = [];

    public LinksViewModel(ConfigStore store, IconCache icons, LinkAdder adder, Scanner.ScannerService scanner, SettingsDialogs dialogs, Localizer text)
    {
        _store = store;
        _icons = icons;
        _adder = adder;
        _scanner = scanner;
        _dialogs = dialogs;
        _text = text;
        Rows = CollectionViewSource.GetDefaultView(_rows);
        Rows.Filter = row => Matches((LinkRow)row);
        Reconcile(_store.Current);
        _store.Changed += OnConfigChanged;
        _text.PropertyChanged += OnLanguageChanged;
    }

    public ICollectionView Rows { get; }

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private int _kindFilter;

    [ObservableProperty]
    private bool _onlyNotPlaced;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editor))]
    private LinkRow? _selected;

    [ObservableProperty]
    private bool _isEmpty;

    public LinkEditorViewModel? Editor { get; private set; }

    public void Select(string linkId)
    {
        Query = "";
        KindFilter = 0;
        OnlyNotPlaced = false;
        Selected = _rows.FirstOrDefault(r => r.Id == linkId);
    }

    public void Dispose()
    {
        _store.Changed -= OnConfigChanged;
        _text.PropertyChanged -= OnLanguageChanged;
    }

    partial void OnQueryChanged(string value) => RefreshView();

    partial void OnKindFilterChanged(int value) => RefreshView();

    partial void OnOnlyNotPlacedChanged(bool value) => RefreshView();

    partial void OnSelectedChanging(LinkRow? value)
    {
        Editor = value is null ? null : new LinkEditorViewModel(value.Id, _store, _icons, _adder, _dialogs, _text);
        _ = Editor?.LoadPreviewAsync();
    }

    [RelayCommand]
    private void AddFromPc() => _scanner.Show();

    private bool Matches(LinkRow row)
    {
        if (KindFilter > 0 && KindFilter <= FilterKinds.Length && row.Kind != FilterKinds[KindFilter - 1])
        {
            return false;
        }
        if (OnlyNotPlaced && row.IsPlaced)
        {
            return false;
        }
        return string.IsNullOrWhiteSpace(Query) || TextSearch.Score(Query, [row.Name, row.Target], []) > 0;
    }

    private void RefreshView()
    {
        Rows.Refresh();
        IsEmpty = Rows.IsEmpty;
    }

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (ReferenceEquals(e.Previous.Links, e.Current.Links) && ReferenceEquals(e.Previous.Placements, e.Current.Placements) && ReferenceEquals(e.Previous.Groups, e.Current.Groups))
        {
            return;
        }
        Reconcile(e.Current);
        Editor?.Refresh();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.KindText = KindName(row.Kind, _text);
        }
        Editor?.Refresh();
    }

    /// <summary>Updates rows in place, so the selection and the editor survive edits.</summary>
    private void Reconcile(AppConfig config)
    {
        var byId = _rows.ToDictionary(r => r.Id);
        var index = 0;
        foreach (var link in config.Links.OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!byId.Remove(link.Id, out var row))
            {
                row = new LinkRow(link.Id);
                _rows.Insert(index, row);
            }
            else if (_rows.IndexOf(row) != index)
            {
                _rows.Move(_rows.IndexOf(row), index);
            }
            var imageChanged = row.Link is null || !Equals(row.Link.Icon, link.Icon) || row.Link.Target != link.Target || row.Link.Kind != link.Kind || row.Link.Name != link.Name;
            row.Link = link;
            row.Name = link.Name;
            row.Kind = link.Kind;
            row.KindText = KindName(link.Kind, _text);
            row.Target = link.Target;
            row.IsPlaced = config.UsageCount(link.Id) > 0;
            if (imageChanged)
            {
                _ = LoadIconAsync(row, link);
            }
            index++;
        }
        foreach (var gone in byId.Values)
        {
            _rows.Remove(gone);
            if (ReferenceEquals(Selected, gone))
            {
                Selected = null;
            }
        }
        RefreshView();
    }

    private async Task LoadIconAsync(LinkRow row, Link link) =>
        row.Icon = await _icons.GetAsync(link) ?? IconCache.LetterImage(link.Name);

    internal static string KindName(LinkKind kind, Localizer text) => text["Kind_" + kind];
}

public enum IconChoice
{
    Auto,
    File,
    Letter,
}

/// <summary>Edits one link; every field is written to the config right away and shows up on every icon (SPEC 4.1).</summary>
public sealed partial class LinkEditorViewModel(string id, ConfigStore store, IconCache icons, LinkAdder adder, SettingsDialogs dialogs, Localizer text) : ObservableObject
{
    private Link Link => store.Current.FindLink(id) ?? new Link { Id = id };

    public string Name
    {
        get => Link.Name;
        set => Change(l => l with { Name = value });
    }

    /// <summary>Committed when the box loses focus: the kind is worked out again from the new target.</summary>
    public string Target
    {
        get => Link.Target;
        set
        {
            var detected = LinkKindDetector.Detect(value, SystemPathProbe.Instance);
            Change(l => detected is null ? l with { Target = value.Trim() } : l with { Target = detected.Target, Kind = detected.Kind });
        }
    }

    public string KindText => text.Format("Link_Kind", LinksViewModel.KindName(Link.Kind, text));

    public bool TargetMissing => Launcher.IsTargetMissing(Link);

    public string Arguments
    {
        get => Link.Arguments;
        set => Change(l => l with { Arguments = value });
    }

    public string WorkingDirectory
    {
        get => Link.WorkingDirectory;
        set => Change(l => l with { WorkingDirectory = value.Trim() });
    }

    public bool CanRunAsAdmin => Launcher.CanRunAsAdmin(Link);

    public bool RunAsAdmin
    {
        get => Link.RunAsAdmin;
        set => Change(l => l with { RunAsAdmin = value });
    }

    public IconChoice IconChoice
    {
        get => Link.Icon.Source switch
        {
            IconSourceKind.File => IconChoice.File,
            IconSourceKind.Letter => IconChoice.Letter,
            _ => IconChoice.Auto,
        };
        set
        {
            if (value == IconChoice.File && string.IsNullOrEmpty(Link.Icon.Path))
            {
                PickIcon();
                OnPropertyChanged();
                return;
            }
            var source = value switch
            {
                IconChoice.File => IconSourceKind.File,
                IconChoice.Letter => IconSourceKind.Letter,
                _ => Link.Kind == LinkKind.Url ? IconSourceKind.Favicon : IconSourceKind.Auto,
            };
            Change(l => l with { Icon = l.Icon with { Source = source } });
        }
    }

    public bool IsWebLink => Link.Kind == LinkKind.Url;

    private IReadOnlyList<BrowserChoice>? _browsers;

    /// <summary>The default browser, then the installed ones (SPEC 4.1); read when the editor first needs it.</summary>
    public IReadOnlyList<BrowserChoice> Browsers => _browsers ??= BrowserChoice.Load(text);

    public BrowserChoice? Browser
    {
        // A browser that is no longer installed shows as the default one, which is what opens the link (BrowserMissingText says why).
        get => BrowserChoice.Find(Browsers, Link.Browser) ?? Browsers[0];
        set => Change(l => l with { Browser = value?.Id });
    }

    /// <summary>Shown when the chosen browser has been uninstalled; the default browser opens the link meanwhile.</summary>
    public string BrowserMissingText =>
        IsWebLink && Link.Browser is { } id && BrowserChoice.Find(Browsers, id) is null ? text.Format("Link_BrowserMissing", id) : "";

    public string IconPath => Link.Icon.Path ?? "";

    [ObservableProperty]
    private ImageSource? _preview;

    public IReadOnlyList<string> Usage
    {
        get
        {
            var config = store.Current;
            var lines = new List<string>();
            var icons = config.Placements.Count(p => p.Type == PlacementType.Link && p.RefId == id);
            if (icons > 0)
            {
                lines.Add(text.Format("Link_UsageIcons", icons));
            }
            lines.AddRange(config.Groups.Where(g => g.LinkIds.Contains(id)).Select(g => text.Format("Link_UsageGroup", g.Name)));
            if (lines.Count == 0)
            {
                lines.Add(text["Links_NotPlaced"]);
            }
            return lines;
        }
    }

    public void Refresh()
    {
        if (store.Current.FindLink(id) is null)
        {
            return;
        }
        OnPropertyChanged(string.Empty);
        _ = LoadPreviewAsync();
    }

    public async Task LoadPreviewAsync()
    {
        var link = Link;
        Preview = await icons.GetAsync(link) ?? IconCache.LetterImage(link.Name);
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog { CheckFileExists = true, DereferenceLinks = false };
        if (dialog.ShowDialog() == true)
        {
            Target = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
        {
            Target = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(Environment.ExpandEnvironmentVariables(WorkingDirectory)))
        {
            dialog.InitialDirectory = Environment.ExpandEnvironmentVariables(WorkingDirectory);
        }
        if (dialog.ShowDialog() == true)
        {
            WorkingDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void PickIcon()
    {
        var dialog = new OpenFileDialog { Filter = $"{text["Link_IconFilter"]}|*.png;*.ico", CheckFileExists = true };
        if (dialog.ShowDialog() == true)
        {
            Change(l => l with { Icon = new IconSpec { Source = IconSourceKind.File, Path = dialog.FileName, Extra = l.Icon.Extra } });
        }
    }

    [RelayCommand]
    private void Open() => Launcher.Open(Link);

    [RelayCommand]
    private void PlaceOnScreen() => adder.Place(PlacementType.Link, id);

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var link = Link;
        var count = store.Current.UsageCount(id);
        var message = text.Format(count == 1 ? "Notice_ConfirmDeleteOne" : "Notice_ConfirmDeleteMany", link.Name, count);
        if (await dialogs.ConfirmAsync(link.Name, message, text["Common_Delete"], danger: true))
        {
            store.Update(c => c.RemoveLink(id));
        }
    }

    private void Change(Func<Link, Link> change) =>
        store.Update(config => config.FindLink(id) is { } link && change(link) is var next && !Equals(next, link) ? config.UpdateLink(next) : config);
}
