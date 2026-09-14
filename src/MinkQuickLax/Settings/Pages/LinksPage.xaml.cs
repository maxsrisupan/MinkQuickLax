using System.Windows.Controls;
using System.Windows.Input;

namespace MinkQuickLax.Settings.Pages;

public partial class LinksPage
{
    public LinksPage()
    {
        InitializeComponent();
    }

    /// <summary>Fields that change the link's kind or command line save on Enter as well as when focus leaves.</summary>
    private void OnCommitOnEnter(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
    }
}
