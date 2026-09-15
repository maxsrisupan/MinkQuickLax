using System.Windows.Input;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.AddWeb;

/// <summary>"Add website" (SPEC 4.1): a small normal window that takes focus, drawn in the current style.</summary>
public partial class AddWebWindow : StyledWindow
{
    public AddWebWindow(AddWebViewModel model, ThemeService theme)
    {
        CaptionHeight = 48;
        UseTheme(theme);
        InitializeComponent();
        DataContext = model;
        model.CloseRequested += Close;
        Closed += (_, _) => model.Dispose();
        Loaded += (_, _) => AddressBox.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
