using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MinkQuickLax.Services;
using MinkQuickLax.Settings.Pages;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Settings;

/// <summary>
/// The settings window (SPEC 4.8): a normal window drawn as a plate in the current style, one page per section,
/// a search box that finds settings in Thai and English, and confirmations shown inside the window.
/// </summary>
public partial class SettingsWindow : StyledWindow, ISettingsDialogHost
{
    private static readonly TimeSpan RevealHighlight = TimeSpan.FromSeconds(1.6);

    private readonly Localizer _text;
    private readonly PreferencesViewModel _preferences;
    private readonly LinksViewModel _links;
    private readonly GroupsViewModel _groups;
    private readonly DataViewModel _data;
    private readonly AboutViewModel _about;
    private readonly Dictionary<SettingsPage, FrameworkElement> _pages = [];
    private (SettingsPage Page, string? Item)? _pending;
    private TaskCompletionSource<bool>? _dialog;

    public SettingsWindow(IServiceProvider services, ThemeService theme, Localizer text)
    {
        _text = text;
        CaptionHeight = 48;
        UseTheme(theme);
        var dialogs = new SettingsDialogs(this);
        _preferences = ActivatorUtilities.CreateInstance<PreferencesViewModel>(services, dialogs);
        _links = ActivatorUtilities.CreateInstance<LinksViewModel>(services, dialogs);
        _groups = ActivatorUtilities.CreateInstance<GroupsViewModel>(services, dialogs);
        _data = ActivatorUtilities.CreateInstance<DataViewModel>(services, dialogs);
        _about = ActivatorUtilities.CreateInstance<AboutViewModel>(services);

        InitializeComponent();
        SearchBox.TextChanged += (_, _) => UpdateSearch();
        SearchBox.PreviewKeyDown += OnSearchKey;
        SearchResults.PreviewMouseLeftButtonUp += (_, _) => Reveal(SearchResults.SelectedItem as SettingsSearchResult);
        SearchResults.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Reveal(SearchResults.SelectedItem as SettingsSearchResult);
                e.Handled = true;
            }
        };
        Loaded += (_, _) => ShowPage(_pending?.Page ?? SettingsPage.Links, _pending?.Item);
    }

    /// <summary>Opens a page; <paramref name="item"/> selects a link or group, or scrolls to a setting.</summary>
    public void ShowPage(SettingsPage page, string? item = null)
    {
        if (!IsLoaded)
        {
            _pending = (page, item);
            return;
        }
        if (NavButtons().FirstOrDefault(b => Equals(b.Tag, page)) is { } button)
        {
            button.IsChecked = true;
        }
        switch (page)
        {
            case SettingsPage.Links when item is not null:
                _links.Select(item);
                break;
            case SettingsPage.Groups when item is not null:
                _groups.Select(item);
                break;
            default:
                if (item is not null)
                {
                    RevealAnchor(Page(page), item);
                }
                break;
        }
    }

    public Task<bool> ConfirmAsync(string? title, string message, string confirmText, bool danger)
    {
        _dialog?.TrySetResult(false);
        _dialog = new TaskCompletionSource<bool>();
        DialogTitle.Text = title ?? "";
        DialogTitle.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
        DialogMessage.Text = message;
        DialogConfirm.Content = confirmText;
        DialogConfirm.Style = (Style)FindResource(danger ? "Skin.DangerOutlineButton" : "Skin.AccentButton");
        DialogLayer.Visibility = Visibility.Visible;
        // The safe choice has focus, so Enter never deletes by accident.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => DialogCancel.Focus());
        return _dialog.Task;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape && DialogLayer.Visibility == Visibility.Visible)
        {
            CloseDialog(false);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _dialog?.TrySetResult(false);
        _preferences.Dispose();
        _links.Dispose();
        _groups.Dispose();
        base.OnClosed(e);
    }

    private IEnumerable<RadioButton> NavButtons() =>
        Nav.Children.OfType<RadioButton>().Concat(LogicalTreeHelper.GetChildren((DependencyObject)Nav.Parent).OfType<RadioButton>());

    private FrameworkElement Page(SettingsPage page)
    {
        if (!_pages.TryGetValue(page, out var element))
        {
            element = page switch
            {
                SettingsPage.Links => new LinksPage { DataContext = _links },
                SettingsPage.Groups => new GroupsPage { DataContext = _groups },
                SettingsPage.Appearance => new AppearancePage { DataContext = _preferences },
                SettingsPage.Visibility => new VisibilityPage { DataContext = _preferences },
                SettingsPage.Behavior => new BehaviorPage { DataContext = _preferences },
                SettingsPage.Position => new PositionPage { DataContext = _preferences },
                SettingsPage.General => new GeneralPage { DataContext = _preferences },
                SettingsPage.Data => new DataPage { DataContext = _data },
                _ => new AboutPage { DataContext = _about },
            };
            _pages[page] = element;
        }
        return element;
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: SettingsPage page })
        {
            return;
        }
        PageHost.Content = Page(page);
        switch (page)
        {
            case SettingsPage.Data:
                _data.Refresh();
                break;
            case SettingsPage.About:
                _about.Refresh();
                break;
            case SettingsPage.General:
                _preferences.Refresh();
                break;
        }
    }

    private void UpdateSearch()
    {
        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            SearchPopup.IsOpen = false;
            return;
        }
        var results = SettingsSearch.Find(query, _text);
        SearchResults.ItemsSource = results;
        SearchResults.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchNothing.Visibility = results.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        SearchPopup.IsOpen = true;
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when SearchPopup.IsOpen && SearchResults.Items.Count > 0:
                SearchResults.SelectedIndex = 0;
                (SearchResults.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                e.Handled = true;
                break;
            case Key.Enter:
                Reveal(SearchResults.Items.Count > 0 ? SearchResults.Items[0] as SettingsSearchResult : null);
                e.Handled = true;
                break;
            case Key.Escape when SearchPopup.IsOpen:
                SearchPopup.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private void Reveal(SettingsSearchResult? result)
    {
        if (result is null)
        {
            return;
        }
        SearchPopup.IsOpen = false;
        ShowPage(result.Page, result.Anchor);
    }

    /// <summary>Scrolls to the setting, outlines it briefly in the accent color and moves focus to its control.</summary>
    private void RevealAnchor(FrameworkElement page, string anchor)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (FindAnchor(page, anchor) is not { } target)
            {
                return;
            }
            target.BringIntoView();
            if (target is SettingRow row)
            {
                row.IsHighlighted = true;
                var timer = new DispatcherTimer { Interval = RevealHighlight };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    row.IsHighlighted = false;
                };
                timer.Start();
            }
            FirstFocusable(target)?.Focus();
        });
    }

    private static FrameworkElement? FindAnchor(DependencyObject root, string anchor)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is FrameworkElement element && SearchAnchor.GetId(element) == anchor)
            {
                return element;
            }
            if (FindAnchor(child, anchor) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static Control? FirstFocusable(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is Control { Focusable: true, IsEnabled: true } control)
            {
                return control;
            }
            if (FirstFocusable(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private void CloseDialog(bool confirmed)
    {
        DialogLayer.Visibility = Visibility.Collapsed;
        var dialog = _dialog;
        _dialog = null;
        dialog?.TrySetResult(confirmed);
    }

    private void OnDialogConfirm(object sender, RoutedEventArgs e) => CloseDialog(true);

    private void OnDialogCancel(object sender, RoutedEventArgs e) => CloseDialog(false);

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
