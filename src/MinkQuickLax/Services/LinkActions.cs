using System.Globalization;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Platform.Shell;
using MinkQuickLax.Surfaces;

namespace MinkQuickLax.Services;

/// <summary>What happens when an icon is clicked or its menu is used (SPEC 4.1, 4.2, 7).</summary>
public sealed partial class LinkActions
{
    private readonly ConfigStore _store;
    private readonly PlacementController _placements;
    private readonly SurfaceHost _surfaces;
    private readonly ThemeService _theme;
    private readonly Localizer _text;
    private readonly ILogger<LinkActions> _logger;

    private readonly ArrangeController _arrange;

    public LinkActions(ConfigStore store, PlacementController placements, ArrangeController arrange, GroupController groups, SurfaceHost surfaces, ThemeService theme, Localizer text, ILogger<LinkActions> logger)
    {
        _store = store;
        _placements = placements;
        _arrange = arrange;
        _surfaces = surfaces;
        _theme = theme;
        _text = text;
        _logger = logger;
        _placements.LaunchRequested += (window, link) => Open(window, link, asAdmin: false);
        _placements.MenuRequested += ShowMenu;
        _placements.GroupMenuRequested += ShowGroupMenu;
        groups.LinkLaunchRequested += (folder, link) => Open(folder, link, asAdmin: false);
        groups.LinkMenuRequested += ShowLinkInGroupMenu;
    }

    public void Open(IconWindow window, Link link, bool asAdmin)
    {
        if (window.IsTargetMissing && Launcher.IsTargetMissing(link))
        {
            ShowMissing(window, link);
            return;
        }
        var result = Launcher.Open(link, asAdmin);
        if (result.Succeeded)
        {
            LogOpened(_logger, link.Kind, link.Target, asAdmin);
            window.PlayLaunch(_theme.Current.ReduceMotion);
            return;
        }
        LogOpenFailed(_logger, link.Kind, link.Target, result.Error, result.Win32Code);
        switch (result.Error)
        {
            case LaunchError.Cancelled:
                return;
            case LaunchError.NotFound:
                window.SetMissing(true);
                ShowMissing(window, link);
                return;
            default:
                ShowError(window, link, result);
                return;
        }
    }

    private void ShowMenu(IconWindow window, Link link, Placement placement)
    {
        var entries = new List<MenuEntry>
        {
            new MenuCommand(_text["Menu_Open"], () => Open(window, link, asAdmin: false)),
        };
        if (Launcher.CanRunAsAdmin(link))
        {
            entries.Add(new MenuCommand(_text["Menu_RunAsAdmin"], () => Open(window, link, asAdmin: true)));
        }
        if (Launcher.CanOpenLocation(link))
        {
            entries.Add(new MenuCommand(_text["Menu_OpenLocation"], () => OpenLocation(window, link)));
        }
        entries.Add(MenuSeparator.Instance);
        entries.Add(new MenuCommand(_text["Menu_PlaceAgain"], () => _store.Update(c => c.AddPlacement(_placements.PlacementNextTo(placement)))));
        entries.Add(new MenuCommand(_text["Menu_AddToGroup"], () => ShowAddToGroupMenu(link, placement)));
        // The link editor opens the settings window (M7).
        entries.Add(new MenuCommand(_text["Menu_Edit"], () => { }, IsEnabled: false));
        entries.Add(new MenuCommand(_text["Menu_Arrange"], _arrange.Enter, IsEnabled: !_arrange.IsArranging));
        entries.Add(MenuSeparator.Instance);
        entries.Add(new MenuCommand(_text["Menu_RemoveFromScreen"], () => _store.Update(c => c.RemovePlacement(placement.Id))));
        entries.Add(new MenuCommand(_text["Menu_DeleteLink"], () => ConfirmDelete(window, link), IsDanger: true));

        _surfaces.ShowMenu(MouseProximityTracker.CursorPosition(), link.Name, entries);
    }

    /// <summary>"Add to group…": pick an existing group, or make a new one next to this icon (SPEC 4.2).</summary>
    private void ShowAddToGroupMenu(Link link, Placement placement)
    {
        var config = _store.Current;
        var entries = new List<MenuEntry>();
        foreach (var group in config.Groups)
        {
            var alreadyIn = group.LinkIds.Contains(link.Id);
            entries.Add(new MenuCommand(group.Name, () => _store.Update(c => c.AddLinkToGroup(group.Id, link.Id)), IsEnabled: !alreadyIn));
        }
        if (entries.Count > 0)
        {
            entries.Add(MenuSeparator.Instance);
        }
        entries.Add(new MenuCommand(_text["Menu_NewGroup"], () =>
        {
            var group = new Group { Name = _text["Group_DefaultName"], LinkIds = [link.Id] };
            var spot = _placements.PlacementNextTo(placement);
            _store.Update(c => c.CreateGroup(group, spot with { Type = PlacementType.Group, RefId = group.Id }));
        }));
        _surfaces.ShowMenu(MouseProximityTracker.CursorPosition(), _text["Menu_AddToGroup"], entries);
    }

    /// <summary>Right-click on a folder (SPEC 4.3). "Open all" is phase 3.</summary>
    private void ShowGroupMenu(IconWindow window, Group group, Placement placement)
    {
        IReadOnlyList<MenuEntry> entries =
        [
            // The group editor lives in the settings window (M7).
            new MenuCommand(_text["Menu_EditGroup"], () => { }, IsEnabled: false),
            new MenuCommand(_text["Menu_Arrange"], _arrange.Enter, IsEnabled: !_arrange.IsArranging),
            MenuSeparator.Instance,
            new MenuCommand(_text["Menu_RemoveFromScreen"], () => _store.Update(c => c.RemovePlacement(placement.Id))),
            new MenuCommand(_text["Menu_DeleteGroup"], () => ConfirmDeleteGroup(window, group), IsDanger: true),
        ];
        _surfaces.ShowMenu(MouseProximityTracker.CursorPosition(), group.Name, entries);
    }

    private void ShowLinkInGroupMenu(IconWindow folder, Link link, Group group)
    {
        var entries = new List<MenuEntry> { new MenuCommand(_text["Menu_Open"], () => Open(folder, link, asAdmin: false)) };
        if (Launcher.CanRunAsAdmin(link))
        {
            entries.Add(new MenuCommand(_text["Menu_RunAsAdmin"], () => Open(folder, link, asAdmin: true)));
        }
        if (Launcher.CanOpenLocation(link))
        {
            entries.Add(new MenuCommand(_text["Menu_OpenLocation"], () => OpenLocation(folder, link)));
        }
        entries.Add(MenuSeparator.Instance);
        entries.Add(new MenuCommand(_text["Menu_RemoveFromGroup"], () => _store.Update(c => c.RemoveLinkFromGroup(group.Id, link.Id))));
        entries.Add(new MenuCommand(_text["Menu_Edit"], () => { }, IsEnabled: false));
        entries.Add(MenuSeparator.Instance);
        entries.Add(new MenuCommand(_text["Menu_DeleteLink"], () => ConfirmDelete(folder, link), IsDanger: true));
        _surfaces.ShowMenu(MouseProximityTracker.CursorPosition(), link.Name, entries);
    }

    private void ConfirmDeleteGroup(IconWindow window, Group group)
    {
        _surfaces.ShowNotice(window.IconRect, Format("Notice_ConfirmDeleteGroup", group.Name),
        [
            new NoticeButton(_text["Common_Cancel"]),
            new NoticeButton(_text["Common_Delete"], () => _store.Update(c => c.RemoveGroup(group.Id)), IsDanger: true),
        ]);
    }

    private void OpenLocation(IconWindow window, Link link)
    {
        var result = Launcher.OpenLocation(link);
        if (!result.Succeeded && result.Error != LaunchError.Cancelled)
        {
            LogOpenFailed(_logger, link.Kind, link.Target, result.Error, result.Win32Code);
            if (result.Error == LaunchError.NotFound)
            {
                ShowMissing(window, link);
            }
            else
            {
                ShowError(window, link, result);
            }
        }
    }

    private void ConfirmDelete(IconWindow window, Link link)
    {
        var count = _store.Current.UsageCount(link.Id);
        var key = count == 1 ? "Notice_ConfirmDeleteOne" : "Notice_ConfirmDeleteMany";
        _surfaces.ShowNotice(window.IconRect, Format(key, link.Name, count),
        [
            new NoticeButton(_text["Common_Cancel"]),
            new NoticeButton(_text["Common_Delete"], () => _store.Update(c => c.RemoveLink(link.Id)), IsDanger: true),
        ]);
    }

    private void ShowMissing(IconWindow window, Link link)
    {
        _surfaces.ShowNotice(window.IconRect, Format("Notice_NotFound", link.Name),
        [
            new NoticeButton(_text["Common_Close"]),
            new NoticeButton(_text["Menu_DeleteLink"], () => ConfirmDelete(window, link), IsDanger: true),
        ]);
    }

    private void ShowError(IconWindow window, Link link, LaunchResult result)
    {
        var message = result.Error switch
        {
            LaunchError.AccessDenied => Format("Notice_AccessDenied", link.Name),
            LaunchError.NoAssociation => Format("Notice_NoAssociation", link.Name),
            LaunchError.Unsupported => _text["Notice_Unsupported"],
            _ => Format("Notice_OpenFailed", link.Name, result.Win32Code),
        };
        _surfaces.ShowNotice(window.IconRect, message, [new NoticeButton(_text["Common_Close"])]);
    }

    private string Format(string key, params object[] args) => string.Format(_text.Culture, _text[key], args);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened {Kind} {Target} (admin: {AsAdmin})")]
    private static partial void LogOpened(ILogger logger, LinkKind kind, string target, bool asAdmin);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not open {Kind} {Target}: {Error} (Win32 {Code})")]
    private static partial void LogOpenFailed(ILogger logger, LinkKind kind, string target, LaunchError error, int code);
}
