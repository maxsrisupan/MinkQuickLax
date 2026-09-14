using System.Windows;
using System.Windows.Controls;
using MinkQuickLax.Services;

namespace MinkQuickLax.Surfaces;

public abstract record MenuEntry;

public sealed record MenuCommand(string Text, Action Execute, bool IsEnabled = true, bool IsDanger = false) : MenuEntry;

public sealed record MenuSeparator : MenuEntry
{
    public static MenuSeparator Instance { get; } = new();
}

/// <summary>Right-click menu for icons and the tray (SPEC 4.2, 4.7, 5.2).</summary>
public sealed class MenuWindow : SurfaceWindow
{
    public MenuWindow(ThemeService theme, string? header, IReadOnlyList<MenuEntry> entries)
        : base(theme, "Skin.Fill.Menu")
    {
        var panel = new StackPanel { MinWidth = 210 };
        if (!string.IsNullOrEmpty(header))
        {
            var title = new TextBlock
            {
                Style = (Style)FindResource("Skin.Text"),
                Text = header,
                FontSize = 11.5,
                Margin = new Thickness(10, 4, 10, 7),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 260,
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Skin.Ink2");
            panel.Children.Add(title);
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

    private static Border Separator()
    {
        var line = new Border { Height = 1, Margin = new Thickness(8, 4, 8, 4), IsHitTestVisible = false };
        line.SetResourceReference(Border.BackgroundProperty, "Skin.Edge");
        return line;
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
