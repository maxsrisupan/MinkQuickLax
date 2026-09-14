using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <param name="Slot">1-based position of the icon, shown as <c>01</c> in HUD and Dot Matrix.</param>
/// <param name="Meta">Kind and target, for example <c>APP · chrome.exe</c>; shown in capitals.</param>
public sealed record TooltipContent(string Name, int Slot, string Meta);

/// <summary>
/// The icon name shown after hovering (SPEC 4.2, 5.2, 5.8, 5.9). Mouse clicks pass through it. Glass shows the name above
/// the icon; HUD and Dot Matrix show a label beside it with the slot number, the name and a meta line.
/// </summary>
public sealed class TooltipWindow : SurfaceWindow
{
    // The plate sits 16 px from the icon; in HUD a 14 px connector line bridges most of that gap.
    private const double ConnectorLength = 14;
    private const double SideGap = 16;
    private static readonly TimeSpan TypeDelay = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan TypeDuration = TimeSpan.FromMilliseconds(400);

    private readonly TextBlock _text;
    private readonly Grid _detail;
    private readonly TextBlock _slot;
    private readonly TextBlock _name;
    private readonly Grid _metaCell;
    private readonly TextBlock _meta;
    private readonly TextBlock _metaSpace;
    private readonly Rectangle _connector;
    private readonly DispatcherTimer _typing = new();
    private string _metaText = "";
    private int _typed;

    public TooltipWindow(ThemeService theme)
        : base(theme, "Skin.Fill.Tooltip")
    {
        var textStyle = (Style)FindResource("Skin.Text");
        _text = new TextBlock { Style = textStyle, FontSize = IconDesign.TooltipFontSize, MaxWidth = 360, TextTrimming = TextTrimming.CharacterEllipsis };

        _slot = new TextBlock { Style = textStyle };
        _name = new TextBlock { Style = textStyle, MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis };
        // The full meta line, invisible, keeps the label from growing while the visible copy types itself out.
        _metaSpace = new TextBlock { Style = textStyle, FontSize = 10, MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis, Visibility = Visibility.Hidden };
        _meta = new TextBlock { Style = textStyle, FontSize = 10, MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis };
        foreach (var line in new[] { _metaSpace, _meta })
        {
            line.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
            line.SetResourceReference(TextBlock.ForegroundProperty, "Skin.Ink2");
        }
        _metaCell = new Grid { Children = { _metaSpace, _meta } };
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _name, _metaCell } };
        Grid.SetColumn(lines, 1);
        _detail = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Auto }, new ColumnDefinition { Width = GridLength.Auto } },
            Children = { _slot, lines },
        };

        _connector = new Rectangle
        {
            Width = ConnectorLength,
            Height = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = new SolidColorBrush(ThemeService.WithAlpha(HudDesign.Cyan, 0.7)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        Root.Children.Add(_connector);
        Body = _text;
        Shape = SurfaceShape.Tooltip;
        BodyPadding = new Thickness(10, 4, 10, 5);
        _typing.Tick += (_, _) => TypeNext();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                _typing.Stop();
            }
        };
    }

    protected override SurfaceBehavior Behavior => SurfaceBehavior.NoActivate | SurfaceBehavior.ClickThrough;

    public void ShowFor(TooltipContent content, PixelRect iconRect, PixelRect workArea, double scale)
    {
        _typing.Stop();
        var look = CurrentLook;
        if (look.Style == StyleSetting.Glass)
        {
            ShowAbove(content.Name, iconRect, workArea, scale);
            return;
        }

        var hud = look.Style == StyleSetting.Hud;
        Body = _detail;
        BodyPadding = hud ? new Thickness(9, 7, 12, 8) : new Thickness(10, 7, 14, 7);
        _slot.Text = content.Slot.ToString("00", CultureInfo.InvariantCulture);
        _slot.SetResourceReference(TextBlock.FontFamilyProperty, hud ? "Font.Mono" : "Font.Dot");
        _slot.SetResourceReference(TextBlock.ForegroundProperty, hud ? "Accent" : "Skin.Ink");
        _slot.FontSize = hud ? 10 : 26;
        _slot.FontWeight = hud ? FontWeights.Normal : FontWeights.ExtraBold;
        // HUD: the small number lines up with the name; Dot Matrix: the big number is centered on both lines.
        _slot.VerticalAlignment = hud ? VerticalAlignment.Top : VerticalAlignment.Center;
        _slot.Margin = new Thickness(0, hud ? 4 : 0, 10, 0);
        _metaCell.Margin = new Thickness(0, hud ? 2 : 1, 0, 0);
        _name.Text = content.Name;
        _name.SetResourceReference(TextBlock.FontFamilyProperty, hud ? "Font.Display" : "Font.Ui");
        _name.FontSize = hud ? 13.5 : 13;
        _name.FontWeight = FontWeights.SemiBold;

        _metaText = content.Meta.ToUpper(Localizer.Instance.Culture);
        _metaSpace.Text = _metaText;
        _metaCell.Visibility = _metaText.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var typeOut = hud && !look.ReduceMotion && _metaText.Length > 0;
        _meta.Text = typeOut ? "" : _metaText;

        _connector.Visibility = hud ? Visibility.Visible : Visibility.Collapsed;
        PutConnector(onRight: true, hud);
        var gap = (int)Math.Round((hud ? SideGap - ConnectorLength : SideGap) * scale);
        ShowPlaced(size =>
        {
            // Right of the icon, or left when there is no room (SPEC 5.8).
            var onRight = iconRect.Right + gap + size.Width <= workArea.Right;
            PutConnector(onRight, hud);
            var x = onRight ? iconRect.Right + gap : iconRect.Left - gap - size.Width;
            var y = iconRect.Center.Y - size.Height / 2;
            var rect = new PixelRect(x, y, x + size.Width, y + size.Height).MoveInside(workArea);
            return new PixelPoint(rect.Left, rect.Top);
        }, animate: false);

        if (typeOut)
        {
            _typed = 0;
            _typing.Interval = TypeDelay;
            _typing.Start();
        }
    }

    /// <summary>Glass: the name alone, above the icon, or below when there is no room above.</summary>
    private void ShowAbove(string name, PixelRect iconRect, PixelRect workArea, double scale)
    {
        Body = _text;
        BodyPadding = new Thickness(10, 4, 10, 5);
        FrameMargin = new Thickness(0);
        _connector.Visibility = Visibility.Collapsed;
        _text.Text = name;
        var gap = (int)Math.Round(IconDesign.TooltipGap * scale);
        ShowPlaced(size =>
        {
            var x = iconRect.Center.X - size.Width / 2;
            var y = iconRect.Top - gap - size.Height;
            if (y < workArea.Top)
            {
                y = iconRect.Bottom + gap;
            }
            var rect = new PixelRect(x, y, x + size.Width, y + size.Height).MoveInside(workArea);
            return new PixelPoint(rect.Left, rect.Top);
        }, animate: false);
    }

    /// <summary>The HUD connector sits in the transparent margin on the icon side of the plate.</summary>
    private void PutConnector(bool onRight, bool hud)
    {
        var length = hud ? ConnectorLength : 0;
        FrameMargin = onRight ? new Thickness(length, 0, 0, 0) : new Thickness(0, 0, length, 0);
        _connector.HorizontalAlignment = onRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private void TypeNext()
    {
        if (_typed == 0)
        {
            _typing.Interval = TimeSpan.FromMilliseconds(Math.Max(10, TypeDuration.TotalMilliseconds / _metaText.Length));
        }
        _typed = Math.Min(_typed + 1, _metaText.Length);
        _meta.Text = _metaText[.._typed];
        if (_typed >= _metaText.Length)
        {
            _typing.Stop();
        }
    }
}
