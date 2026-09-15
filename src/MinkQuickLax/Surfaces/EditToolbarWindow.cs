using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <summary>
/// Toolbar at the top of the screen in edit mode (SPEC 4.4, 5.2): add, tidy up, cancel, done. It takes focus so the
/// keyboard shortcuts work; it still stays out of the taskbar and Alt+Tab.
/// </summary>
public sealed class EditToolbarWindow : SurfaceWindow
{
    private readonly TranslateTransform _slide = new();
    private readonly Border _marker;
    private readonly LabelText _title;

    public EditToolbarWindow(ThemeService theme, Localizer text, Action add, bool canAdd, Action tidy, Action cancel, Action done)
        : base(theme, "Skin.Fill.Panel")
    {
        ShowActivated = true;
        Focusable = true;
        _marker = new Border { Width = 7, Height = 7, Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        _title = new LabelText { Style = (Style)FindResource("Skin.Text"), Source = text["Arrange_Title"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        ApplyStatusStyle(CurrentLook);

        var row = new StackPanel { Orientation = Orientation.Horizontal, RenderTransform = _slide };
        row.Children.Add(_marker);
        row.Children.Add(_title);
        row.Children.Add(MakeButton("Skin.Button", text["Arrange_Add"], add, canAdd));
        row.Children.Add(MakeButton("Skin.Button", text["Arrange_Tidy"], tidy, true));
        row.Children.Add(MakeButton("Skin.Button", text["Arrange_Cancel"], cancel, true));
        row.Children.Add(MakeButton("Skin.AccentButton", text["Arrange_Done"], done, true));
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

    protected override void OnLookChanged(Look look)
    {
        base.OnLookChanged(look);
        ApplyStatusStyle(look);
    }

    /// <summary>
    /// The status at the left (SPEC 5.2, 5.8, 5.9). Glass: a guide-colored dot and plain text. HUD: a glowing amber
    /// square that blinks and amber mono text. Dot Matrix: a blinking red dot and mono text.
    /// </summary>
    private void ApplyStatusStyle(Look look)
    {
        var style = look.Style;
        _marker.CornerRadius = new CornerRadius(style == StyleSetting.Hud ? 0 : 3.5);
        _marker.SetResourceReference(Border.BackgroundProperty, style switch { StyleSetting.Hud => "Skin.Amber", StyleSetting.Dot => "Danger", _ => "Guide" });
        _marker.Effect = style == StyleSetting.Hud ? new DropShadowEffect { ShadowDepth = 0, BlurRadius = 8, Opacity = 0.9, Color = HudDesign.Amber } : null;
        _marker.BeginAnimation(OpacityProperty, null);
        if (style != StyleSetting.Glass && !look.ReduceMotion)
        {
            var period = style == StyleSetting.Hud ? IconStyleDesign.HudMarkerBlink : IconStyleDesign.DotMarkerBlink;
            var blink = new DoubleAnimationUsingKeyFrames { Duration = period, RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(blink, IconStyleDesign.BlinkFrameRate);
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.5)));
            _marker.BeginAnimation(OpacityProperty, blink);
        }

        _title.FontSize = style == StyleSetting.Glass ? 13 : 11;
        if (style == StyleSetting.Glass)
        {
            _title.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Ui");
        }
        else
        {
            _title.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
        }
        _title.SetResourceReference(TextBlock.ForegroundProperty, style == StyleSetting.Hud ? "Skin.Amber" : "Skin.Ink");
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
