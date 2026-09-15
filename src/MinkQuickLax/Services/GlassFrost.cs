using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MinkQuickLax.Core.Layout;
using MinkQuickLax.Platform.Shell;

namespace MinkQuickLax.Services;

/// <summary>
/// Frosted Glass without Windows blur (SPEC 5.5). When Windows will not blur behind a window (Transparency effects off,
/// or Windows older than 11 22H2), Glass plates show a heavily blurred copy of the part of the desktop picture behind
/// them. Each monitor gets one small blurred picture, made the first time a plate needs it.
/// </summary>
public sealed class GlassFrost
{
    /// <summary>The blurred picture is this wide whatever the monitor size; a plate stretches its part of it.</summary>
    public const int PictureWidth = 192;

    /// <summary>Blur in picture pixels: about 1/20 of the monitor width, so shapes turn into soft color.</summary>
    public const double BlurRadius = 10;

    /// <summary>Color boost after blurring, like the saturation in Windows acrylic (mockup: saturate 170%).</summary>
    public const double Saturation = 1.5;

    private const int Pad = 24;

    private readonly Dictionary<PixelRect, (BitmapSource Picture, double Scale)> _pictures = [];
    private IReadOnlyList<DesktopBackground> _backgrounds = [];
    private string _signature = "";

    /// <summary>Reads the desktop backgrounds again. True when the pictures or monitors changed since last time.</summary>
    public bool Refresh()
    {
        var backgrounds = DesktopBackgrounds.Read();
        var signature = string.Join("|", backgrounds.Select(b =>
            string.Create(CultureInfo.InvariantCulture, $"{b.Monitor};{b.ImagePath};{WriteTime(b.ImagePath)};{b.Fit};{b.BackgroundRgb}")));
        if (signature == _signature)
        {
            return false;
        }
        _signature = signature;
        _backgrounds = backgrounds;
        _pictures.Clear();
        return true;
    }

    /// <summary>The frost for a window at <paramref name="window"/> (physical pixels), or null when its monitor is unknown.</summary>
    public Brush? BrushFor(PixelRect window)
    {
        if (_signature.Length == 0)
        {
            Refresh();
        }
        var background = _backgrounds.FirstOrDefault(b => b.Monitor.Contains(window.Center));
        if (background is null)
        {
            return null;
        }
        if (!_pictures.TryGetValue(background.Monitor, out var frosted))
        {
            frosted = Build(background, Desktop());
            _pictures[background.Monitor] = frosted;
        }
        var m = background.Monitor;
        var s = frosted.Scale;
        var brush = new ImageBrush(frosted.Picture)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect((window.Left - m.Left) * s, (window.Top - m.Top) * s, window.Width * s, window.Height * s),
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        return brush;
    }

    private PixelRect Desktop() => _backgrounds.Aggregate(_backgrounds[0].Monitor, (all, b) => new PixelRect(
        Math.Min(all.Left, b.Monitor.Left), Math.Min(all.Top, b.Monitor.Top), Math.Max(all.Right, b.Monitor.Right), Math.Max(all.Bottom, b.Monitor.Bottom)));

    private static (BitmapSource Picture, double Scale) Build(DesktopBackground background, PixelRect desktop)
    {
        var m = background.Monitor;
        var scale = (double)PictureWidth / m.Width;
        var width = PictureWidth;
        var height = Math.Max(1, (int)Math.Round(m.Height * scale));
        var rgb = background.BackgroundRgb;
        var color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            // The margin around the picture keeps the blur from fading to transparent at the monitor edges.
            dc.PushTransform(new TranslateTransform(Pad, Pad));
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(-Pad, -Pad, width + 2 * Pad, height + 2 * Pad));
            if (background.ImagePath is { } path && ImageSize(path) is { } size)
            {
                var placed = WallpaperLayout.Place(background.Fit, m, size, desktop);
                var target = new Rect((placed.Left - m.Left) * scale, (placed.Top - m.Top) * scale, placed.Width * scale, placed.Height * scale);
                if (Load(path, (int)Math.Clamp(Math.Ceiling(target.Width * 2), 8, 1024)) is { } image)
                {
                    if (background.Fit == WallpaperFit.Tile)
                    {
                        var tiles = new ImageBrush(image) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = target, Stretch = Stretch.Fill };
                        dc.DrawRectangle(tiles, null, new Rect(-Pad, -Pad, width + 2 * Pad, height + 2 * Pad));
                    }
                    else
                    {
                        dc.DrawImage(image, target);
                    }
                }
            }
        }
        drawing.Effect = new BlurEffect { Radius = BlurRadius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
        var root = new ContainerVisual { Children = { drawing } };
        var rendered = new RenderTargetBitmap(width + 2 * Pad, height + 2 * Pad, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(root);

        var stride = width * 4;
        var pixels = new byte[stride * height];
        rendered.CopyPixels(new Int32Rect(Pad, Pad, width, height), pixels, stride, 0);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            double b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            var gray = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            pixels[i] = Boost(gray, b);
            pixels[i + 1] = Boost(gray, g);
            pixels[i + 2] = Boost(gray, r);
        }
        var picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        picture.Freeze();
        return (picture, scale);
    }

    private static byte Boost(double gray, double channel) => (byte)Math.Round(Math.Clamp(gray + (channel - gray) * Saturation, 0, 255));

    /// <summary>The picture's own size, read from its header without decoding it.</summary>
    private static PixelSize? ImageSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            return new PixelSize(frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static BitmapImage? Load(string path, int decodeWidth)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.DecodePixelWidth = decodeWidth;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static long WriteTime(string? path)
    {
        try
        {
            return path is null ? 0 : File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 0;
        }
    }
}
