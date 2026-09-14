using System.Windows;
using System.Windows.Controls;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

public abstract record MenuEntry;

public sealed record MenuCommand(string Text, Action Execute, bool IsEnabled = true, bool IsDanger = false) : MenuEntry;

public sealed record MenuSeparator : MenuEntry
{
    public static MenuSeparator Instance { get; } = new();
}

/// <summary>Right-click menu for icons and the tray (SPEC 4.2, 4.7, 5.2, 5.8, 5.9).</summary>
public sealed class MenuWindow : SurfaceWindow
{
    public MenuWindow(ThemeService theme, string? header, IReadOnlyList<MenuEntry> entries)
        : base(theme, "Skin.Fill.Menu")
    {
        var style = CurrentLook.Style;
        var panel = new StackPanel { MinWidth = style == StyleSetting.Hud ? 232 : 210 };
        if (!string.IsNullOrEmpty(header))
        {
            panel.Children.Add(Header(header, style));
            panel.Children.Add(Separator());
        }

        foreach (var entry in entries)
        {
            switch (entry)
            {
                case MenuSeparator:
                    panel.Children.Add(Separator());
                    break;
                case MenuCommand command:
                    var button = new Button
                    {
                        Style = (Style)FindResource("Skin.MenuRow"),
                        Content = command.Text,
                        IsEnabled = command.IsEnabled,
                    };
                    if (command.IsDanger)
                    {
                        button.SetResourceReference(ForegroundProperty, "Danger");
                    }
                    button.Click += (_, _) =>
                    {
                        Close();
                        command.Execute();
                    };
                    panel.Children.Add(button);
                    break;
            }
        }
        Body = panel;
        BodyPadding = new Thickness(6);
    }

    /// <summary>Glass: small secondary text. HUD: cyan mono capitals. Dot Matrix: mono capitals after a red dot.</summary>
    private StackPanel Header(string header, StyleSetting style)
    {
        var title = new LabelText
        {
            Style = (Style)FindResource("Skin.Text"),
            Source = header,
            FontSize = style == StyleSetting.Glass ? 11.5 : 10.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Skin.SetKind(title, style);
        title.SetResourceReference(TextBlock.ForegroundProperty, style switch { StyleSetting.Hud => "Accent", StyleSetting.Dot => "Skin.Ink", _ => "Skin.Ink2" });
        if (style != StyleSetting.Glass)
        {
            title.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 4, 10, 7) };
        if (style == StyleSetting.Dot)
        {
            var dot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Danger");
            row.Children.Add(dot);
        }
        row.Children.Add(title);
        return row;
    }

    private StyledSeparator Separator()
    {
        var separator = new StyledSeparator { Margin = new Thickness(8, 3, 8, 3) };
        if (CurrentLook.Style == StyleSetting.Hud && !CurrentLook.HighContrast)
        {
            // SPEC 5.8: menu dividers are fainter than plate edges.
            separator.Brush = new System.Windows.Media.SolidColorBrush(ThemeService.WithAlpha(HudDesign.Cyan, 0.18));
        }
        return separator;
    }
}

public sealed record NoticeButton(string Text, Action? Execute = null, bool IsPrimary = false, bool IsDanger = false);

/// <summary>A short message with buttons, shown next to an icon or the tray (errors, confirmations).</summary>
public sealed class NoticeWindow : SurfaceWindow
{
    public NoticeWindow(ThemeService theme, string message, IReadOnlyList<NoticeButton> buttons)
        : base(theme, "Skin.Fill.Notice")
    {
        var text = new TextBlock
        {
            Style = (Style)FindResource("Skin.Text"),
            Text = message,
            FontSize = 13.5,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320,
            Margin = new Thickness(10, 6, 10, 10),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var b in buttons)
        {
            var styleKey = b.IsPrimary ? "Skin.AccentButton" : b.IsDanger ? "Skin.DangerButton" : "Skin.Button";
            var button = new Button { Style = (Style)FindResource(styleKey), Content = b.Text, Margin = new Thickness(4, 0, 0, 0) };
            button.Click += (_, _) =>
            {
                Close();
                b.Execute?.Invoke();
            };
            row.Children.Add(button);
        }
        Body = new StackPanel { Children = { text, row } };
        BodyPadding = new Thickness(8);
    }
}
