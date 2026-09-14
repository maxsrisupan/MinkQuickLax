using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;
using MinkQuickLax.Styles.Glass;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// Toolbar at the top of the screen in edit mode (SPEC 4.4, 5.2): add, tidy up, cancel, done. It takes focus so the
/// keyboard shortcuts work; it still stays out of the taskbar and Alt+Tab.
/// </summary>
public sealed class EditToolbarWindow : GlassSurfaceWindow
{
    private readonly TranslateTransform _slide = new();

    public EditToolbarWindow(ThemeService theme, Localizer text, Action add, bool canAdd, Action tidy, Action cancel, Action done)
        : base(theme, "Glass.Fill.Panel")
    {
        ShowActivated = true;
        Focusable = true;
        var dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(Shape.FillProperty, "Guide");
        var title = new TextBlock { Style = (Style)FindResource("Glass.Text"), Text = text["Arrange_Title"], FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        var row = new StackPanel { Orientation = Orientation.Horizontal, RenderTransform = _slide };
        row.Children.Add(dot);
        row.Children.Add(title);
        row.Children.Add(MakeButton("Glass.Button", text["Arrange_Add"], add, canAdd));
        row.Children.Add(MakeButton("Glass.Button", text["Arrange_Tidy"], tidy, true));
        row.Children.Add(MakeButton("Glass.Button", text["Arrange_Cancel"], cancel, true));
        row.Children.Add(MakeButton("Glass.AccentButton", text["Arrange_Done"], done, true));
        Body = row;
        BodyPadding = new Thickness(6);
    }

    /// <summary>Key presses while the toolbar has focus (arrows, Delete, Ctrl+Z/Y, Enter).</summary>
    public event Action<Key, ModifierKeys>? KeyPressed;

    protected override SurfaceBehavior Behavior => SurfaceBehavior.None;

    public void ShowOn(PixelRect workArea, double scale)
    {
        var gap = (int)Math.Round(Motion.ToolbarTopGap * scale);
        ShowPlaced(size => new PixelPoint(workArea.Center.X - size.Width / 2, workArea.Top + gap), animate: true);
        Activate();
    }

    /// <summary>Clicking an icon moves Win32 focus to that icon's window; take it back so the shortcuts keep working.</summary>
    public void TakeKeyboardFocus()
    {
        if (IsVisible)
        {
            Activate();
            Keyboard.Focus(this);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // None of the buttons take focus, so hold it on the window itself or key presses go nowhere.
        Keyboard.Focus(this);
    }

    protected override void AnimateIn()
    {
        var drop = new DoubleAnimation(-40, 0, Motion.ToolbarIn) { EasingFunction = Motion.Spring };
        _slide.BeginAnimation(TranslateTransform.YProperty, drop);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        KeyPressed?.Invoke(key, Keyboard.Modifiers);
        if (key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Delete or Key.Z or Key.Y or Key.Enter or Key.Escape)
        {
            e.Handled = true;
        }
    }

    private Button MakeButton(string style, string label, Action action, bool enabled)
    {
        var button = new Button { Style = (Style)FindResource(style), Content = label, IsEnabled = enabled, Margin = new Thickness(2, 0, 0, 0) };
        button.Click += (_, _) => action();
        return button;
    }
}
