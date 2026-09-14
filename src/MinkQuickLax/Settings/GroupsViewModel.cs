using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Services;

namespace MinkQuickLax.Settings;

public sealed partial class GroupRow(string id) : ObservableObject
{
    public string Id { get; } = id;

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private int _count;
}

/// <summary>A link inside the group being edited.</summary>
public sealed partial class GroupLinkRow(string linkId, string name) : ObservableObject
{
    public string LinkId { get; } = linkId;

    public string Name { get; } = name;

    [ObservableProperty]
    private ImageSource? _icon;
}

/// <summary>A link that can be added to the group being edited.</summary>
public sealed record LinkChoice(string Id, string Name);

/// <summary>The Groups page (SPEC 4.8): create and delete groups, and change their name, columns, labels and link order.</summary>
public sealed partial class GroupsViewModel : ObservableObject, IDisposable
{
    private readonly ConfigStore _store;
    private readonly IconCache _icons;
    private readonly LinkAdder _adder;
    private readonly SettingsDialogs _dialogs;
    private readonly Localizer _text;

    public GroupsViewModel(ConfigStore store, IconCache icons, LinkAdder adder, SettingsDialogs dialogs, Localizer text)
    {
        _store = store;
        _icons = icons;
        _adder = adder;
        _dialogs = dialogs;
        _text = text;
        Reconcile(_store.Current);
        _store.Changed += OnConfigChanged;
    }

    public ObservableCollection<GroupRow> Groups { get; } = [];

    public ObservableCollection<GroupLinkRow> Links { get; } = [];

    public ObservableCollection<LinkChoice> AvailableLinks { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(Name), nameof(Columns), nameof(ShowLabels), nameof(IsPlaced))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(PlaceOnScreenCommand))]
    private GroupRow? _selected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(RemoveLinkCommand))]
    private GroupLinkRow? _selectedLink;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddLinkCommand))]
    private LinkChoice? _linkToAdd;

    public bool HasSelection => Selected is not null;

    private Group? Group => Selected is null ? null : _store.Current.FindGroup(Selected.Id);

    public string Name
    {
        get => Group?.Name ?? "";
        set => Change(g => g with { Name = value });
    }

    public double Columns
    {
        get => Group?.Columns ?? 4;
        set => Change(g => g with { Columns = Math.Clamp((int)Math.Round(value), SettingsLimits.GroupColumnsMin, SettingsLimits.GroupColumnsMax) });
    }

    public bool ShowLabels
    {
        get => Group?.ShowLabels ?? true;
        set => Change(g => g with { ShowLabels = value });
    }

    public bool IsPlaced => Selected is not null && _store.Current.Placements.Any(p => p.Type == PlacementType.Group && p.RefId == Selected.Id);

    public void Select(string groupId) => Selected = Groups.FirstOrDefault(g => g.Id == groupId);

    public void Dispose() => _store.Changed -= OnConfigChanged;

    partial void OnSelectedChanged(GroupRow? value) => RebuildLinks();

    [RelayCommand]
    private void New()
    {
        var group = new Group { Name = _text["Group_DefaultName"] };
        _store.Update(c => c with { Groups = [.. c.Groups, group] });
        _adder.Place(PlacementType.Group, group.Id);
        Select(group.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAsync()
    {
        if (Group is not { } group)
        {
            return;
        }
        if (await _dialogs.ConfirmAsync(group.Name, _text.Format("Notice_ConfirmDeleteGroup", group.Name), _text["Common_Delete"], danger: true))
        {
            _store.Update(c => c.RemoveGroup(group.Id));
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void PlaceOnScreen()
    {
        if (Selected is { } row && !IsPlaced)
        {
            _adder.Place(PlacementType.Group, row.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    [RelayCommand(CanExecute = nameof(HasSelectedLink))]
    private void RemoveLink()
    {
        if (Selected is { } group && SelectedLink is { } link)
        {
            _store.Update(c => c.RemoveLinkFromGroup(group.Id, link.LinkId));
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddLink))]
    private void AddLink()
    {
        if (Selected is { } group && LinkToAdd is { } link)
        {
            _store.Update(c => c.AddLinkToGroup(group.Id, link.Id));
            LinkToAdd = null;
        }
    }

    private bool HasSelectedLink => SelectedLink is not null;

    private bool CanMoveUp => SelectedLink is not null && Links.IndexOf(SelectedLink) > 0;

    private bool CanMoveDown => SelectedLink is not null && Links.IndexOf(SelectedLink) < Links.Count - 1;

    private bool CanAddLink => Selected is not null && LinkToAdd is not null;

    private void Move(int by)
    {
        if (Selected is not { } group || SelectedLink is not { } link)
        {
            return;
        }
        var index = Links.IndexOf(link) + by;
        _store.Update(c => c.MoveLinkInGroup(group.Id, link.LinkId, index));
    }

    private void Change(Func<Group, Group> change) =>
        _store.Update(config => Group is { } group && config.FindGroup(group.Id) is { } current && change(current) is var next && !Equals(next, current) ? config.UpdateGroup(next) : config);

    private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
    {
        if (ReferenceEquals(e.Previous.Groups, e.Current.Groups) && ReferenceEquals(e.Previous.Links, e.Current.Links) && ReferenceEquals(e.Previous.Placements, e.Current.Placements))
        {
            return;
        }
        Reconcile(e.Current);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(ShowLabels));
        OnPropertyChanged(nameof(IsPlaced));
        var before = Selected is null ? null : e.Previous.FindGroup(Selected.Id);
        if (!ReferenceEquals(e.Previous.Links, e.Current.Links) || !(before?.LinkIds ?? []).SequenceEqual(Group?.LinkIds ?? []))
        {
            // Only when the list itself changed, so typing a name does not rebuild it.
            RebuildLinks();
        }
    }

    private void Reconcile(AppConfig config)
    {
        var byId = Groups.ToDictionary(g => g.Id);
        var index = 0;
        foreach (var group in config.Groups)
        {
            if (!byId.Remove(group.Id, out var row))
            {
                row = new GroupRow(group.Id);
                Groups.Insert(index, row);
            }
            else if (Groups.IndexOf(row) != index)
            {
                Groups.Move(Groups.IndexOf(row), index);
            }
            row.Name = group.Name;
            row.Count = group.LinkIds.Count;
            index++;
        }
        foreach (var gone in byId.Values)
        {
            Groups.Remove(gone);
            if (ReferenceEquals(Selected, gone))
            {
                Selected = null;
            }
        }
    }

    private void RebuildLinks()
    {
        var config = _store.Current;
        var group = Group;
        var keep = SelectedLink?.LinkId;
        Links.Clear();
        AvailableLinks.Clear();
        if (group is null)
        {
            return;
        }
        foreach (var link in group.LinkIds.Select(config.FindLink).OfType<Link>())
        {
            var row = new GroupLinkRow(link.Id, link.Name);
            Links.Add(row);
            _ = LoadIconAsync(row, link);
        }
        foreach (var link in config.Links.Where(l => !group.LinkIds.Contains(l.Id)).OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            AvailableLinks.Add(new LinkChoice(link.Id, link.Name));
        }
        SelectedLink = Links.FirstOrDefault(l => l.LinkId == keep);
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadIconAsync(GroupLinkRow row, Link link) =>
        row.Icon = await _icons.GetAsync(link) ?? IconCache.LetterImage(link.Name);
}
