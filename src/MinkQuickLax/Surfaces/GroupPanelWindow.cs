using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Services;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Surfaces;

/// <summary>One link inside an open group panel.</summary>
public sealed class PanelItem : Border
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };

    public PanelItem(Link link, double iconSize, bool showLabel)
    {
        Link = link;
        Width = iconSize + IconDesign.PanelCellExtra;
        Padding = new Thickness(0, 6, 0, 4);
        CornerRadius = new CornerRadius(8);
        Background = Brushes.Transparent;
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        _image.Width = iconSize;
        _image.Height = iconSize;
        _image.Source = IconCache.LetterImage(link.Name);
        var stack = new StackPanel { Children = { _image } };
        if (showLabel)
        {
            var label = new TextBlock
            {
                Style = (Style)FindResource("Skin.Text"),
                Text = link.Name,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = iconSize + IconDesign.LabelExtraWidth,
                Margin = new Thickness(0, 4, 0, 0),
            };
            stack.Children.Add(label);
        }
        Child = stack;
        MouseEnter += (_, _) => SetResourceReference(BackgroundProperty, "Skin.HoverFill");
        MouseLeave += (_, _) => Background = Brushes.Transparent;
    }

    public Link Link { get; }

    public ImageSource Icon
    {
        get => _image.Source;
        set => _image.Source = value;
    }
}

/// <summary>An open group: its name and a grid of links, unfolding toward free space (SPEC 4.3, 5.2, 5.4).</summary>
public sealed class GroupPanelWindow : SurfaceWindow
{
    private readonly WrapPanel _grid;
    private readonly ScaleTransform _unfold = new(1, 1);
    private Point _origin = new(0, 0);

    public GroupPanelWindow(ThemeService theme, Localizer text, Group group, IReadOnlyList<Link> links, double iconSize, int columns)
        : base(theme, "Skin.Fill.Panel")
    {
        Group = group;
        var title = new TextBlock
        {
            Style = (Style)FindResource("Skin.Text"),
            Text = group.Name,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 2, 6, 8),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var cell = iconSize + IconDesign.PanelCellExtra;
        _grid = new WrapPanel { Width = cell * Math.Max(1, Math.Min(columns, Math.Max(links.Count, 1))) };
        foreach (var link in links)
        {
            _grid.Children.Add(new PanelItem(link, iconSize, group.ShowLabels));
        }
        var content = new StackPanel { Children = { title } };
        if (links.Count == 0)
        {
            content.Children.Add(new TextBlock
            {
                Style = (Style)FindResource("Skin.Text"),
                Text = text["Panel_Empty"],
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260,
                Margin = new Thickness(6, 0, 6, 6),
            });
            content.SetResourceReference(TextBlock.ForegroundProperty, "Skin.Ink2");
        }
        else
        {
            content.Children.Add(_grid);
        }
        content.RenderTransform = _unfold;
        Body = content;
        BodyPadding = new Thickness(10);
    }

    public Group Group { get; }

    public IEnumerable<PanelItem> Items => _grid.Children.OfType<PanelItem>();

    /// <summary>Picks the side with room: beside the folder toward the larger free space, else above or below.</summary>
    public static PixelPoint PlaceBeside(PixelRect folder, PixelSize size, PixelRect area, int gap, out Point unfoldOrigin)
    {
        var roomRight = area.Right - folder.Right;
        var roomLeft = folder.Left - area.Left;
        int x;
        int y = folder.Top;
        if (roomRight >= size.Width + gap || roomLeft >= size.Width + gap)
        {
            var toRight = roomRight >= roomLeft;
            x = toRight ? folder.Right + gap : folder.Left - gap - size.Width;
            unfoldOrigin = new Point(toRight ? 0 : 1, 0);
        }
        else
        {
            x = folder.Center.X - size.Width / 2;
            var below = area.Bottom - folder.Bottom >= folder.Top - area.Top;
            y = below ? folder.Bottom + gap : folder.Top - gap - size.Height;
            unfoldOrigin = new Point(0.5, below ? 0 : 1);
        }
        var rect = new PixelRect(x, y, x + size.Width, y + size.Height).MoveInside(area);
        return new PixelPoint(rect.Left, rect.Top);
    }

    public void SetUnfoldOrigin(Point origin) => _origin = origin;

    /// <summary>The index a dropped item should take, from a point in physical screen pixels.</summary>
    public int IndexAt(PixelPoint screenPoint)
    {
        var items = Items.ToList();
        if (items.Count == 0)
        {
            return 0;
        }
        var local = _grid.PointFromScreen(new Point(screenPoint.X, screenPoint.Y));
        var cellWidth = items[0].ActualWidth;
        var cellHeight = items[0].ActualHeight;
        var columns = Math.Max(1, (int)Math.Round(_grid.Width / cellWidth));
        var column = Math.Clamp((int)(local.X / cellWidth), 0, columns - 1);
        var row = Math.Max(0, (int)(local.Y / cellHeight));
        return Math.Clamp(row * columns + column, 0, items.Count - 1);
    }

    protected override void AnimateIn()
    {
        if (Body is not FrameworkElement content)
        {
            return;
        }
        content.RenderTransformOrigin = _origin;
        var grow = new DoubleAnimation(0.6, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = Motion.Out };
        _unfold.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _unfold.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
    }

    public static bool IsInside(FrameworkElement element, MouseEventArgs e)
    {
        var p = e.GetPosition(element);
        return p.X >= 0 && p.Y >= 0 && p.X < element.ActualWidth && p.Y < element.ActualHeight;
    }
}
