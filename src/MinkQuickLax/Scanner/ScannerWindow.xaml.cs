using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Scanner;

/// <summary>"Add from this PC" (SPEC 4.1, 5.2): a normal window that can take focus, drawn in the current style.</summary>
public partial class ScannerWindow : StyledWindow
{
    private readonly ScannerViewModel _model;

    public ScannerWindow(ScannerViewModel model, ThemeService theme)
    {
        _model = model;
        CaptionHeight = 64;
        UseTheme(theme);
        InitializeComponent();
        DataContext = model;
        model.CloseRequested += Close;
        Closed += (_, _) => model.Dispose();
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            await model.ScanAsync();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnPickFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { CheckFileExists = true, Multiselect = true, DereferenceLinks = false };
        if (dialog.ShowDialog(this) == true)
        {
            foreach (var file in dialog.FileNames)
            {
                _model.AddManual(file);
            }
        }
    }

    private void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true };
        if (dialog.ShowDialog(this) == true)
        {
            foreach (var folder in dialog.FolderNames)
            {
                _model.AddManual(folder);
            }
        }
    }

    private void OnUrlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _model.AddUrlCommand.Execute(null);
            e.Handled = true;
        }
    }
}

/// <summary>Binds a <see cref="ScanFilter"/> to one radio button of the segmented filter.</summary>
public sealed class FilterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ScanFilter filter && string.Equals(filter.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && Enum.TryParse<ScanFilter>(parameter as string, out var filter) ? filter : Binding.DoNothing;
}
