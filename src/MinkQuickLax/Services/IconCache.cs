using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Model;
using MinkQuickLax.Platform.Shell;
using MinkQuickLax.Styles;

namespace MinkQuickLax.Services;

/// <summary>
/// Link images: shell icons extracted off the UI thread and kept as 256 px PNGs in
/// %LocalAppData%\MinkQuickLax\cache\icons, custom image files, or a vector letter icon.
/// </summary>
public sealed partial class IconCache : IDisposable
{
    private readonly string _directory;
    private readonly ILogger<IconCache> _logger;
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _loaded = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _extractors = new(2);
    private readonly FaviconFetcher _favicons;

    public IconCache(FaviconFetcher favicons, ILogger<IconCache> logger)
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinkQuickLax", "cache", "icons"), favicons, logger)
    {
    }

    public IconCache(string directory, FaviconFetcher favicons, ILogger<IconCache> logger)
    {
        _directory = directory;
        _favicons = favicons;
        _logger = logger;
    }

    /// <summary>The image for a link, or null when it should be drawn as a letter icon.</summary>
    public Task<ImageSource?> GetAsync(Link link)
    {
        if (link.Icon.Source == IconSourceKind.Letter || link.Kind is LinkKind.MsSettings or LinkKind.Unknown)
        {
            return Task.FromResult<ImageSource?>(null);
        }
        return _loaded.GetOrAdd(KeyFor(link), _ => LoadAsync(link));
    }

    /// <summary>Stores an icon captured while scanning, so a new link shows exactly what Start shows.</summary>
    public void Seed(Link link, IconBitmap bitmap)
    {
        var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.Pixels, bitmap.Width * 4);
        source.Freeze();
        SavePng(source, CachePath(link));
        _loaded[KeyFor(link)] = Task.FromResult<ImageSource?>(source);
    }

    public void Dispose() => _extractors.Dispose();

    /// <summary>Forgets a failed or changed image so the next request tries again.</summary>
    public void Invalidate(Link link) => _loaded.TryRemove(KeyFor(link), out _);

    public static DrawingImage LetterImage(string name)
    {
        var icon = LetterIcon.For(name);
        var (from, to) = IconDesign.LetterPalette[icon.PaletteIndex];
        const double Size = 100;
        var radius = Size * IconDesign.CornerRatio;
        var bounds = new Rect(0, 0, Size, Size);

        // 150°: from the top-left toward the bottom-right, a little steeper than a diagonal.
        var fill = new LinearGradientBrush(from, to, new Point(0.21, 0), new Point(0.79, 1));
        // Fades out by the middle, so there is no hard line where the top half ends.
        var gloss = new LinearGradientBrush(Color.FromArgb(77, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(0, 0.5));
        var text = new FormattedText(icon.Text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface((FontFamily)Application.Current.Resources["Font.Display"], FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            Size * IconDesign.LetterSizeRatio, Brushes.White, 1.0);

        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRoundedRectangle(fill, null, bounds, radius, radius);
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, Size, Size / 2)));
            dc.DrawRoundedRectangle(gloss, null, bounds, radius, radius);
            dc.Pop();
            // Glass edge (SPEC 5.3): a rim bright at the top-left and bottom-right, and a top line fading to the right.
            var rim = GlassLight.Rim(Color.FromArgb(140, 255, 255, 255), Color.FromArgb(28, 255, 255, 255));
            dc.DrawRoundedRectangle(null, new Pen(rim, 2), new Rect(1, 1, Size - 2, Size - 2), radius - 1, radius - 1);
            var top = new LinearGradientBrush(
                [new GradientStop(Color.FromArgb(128, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), GlassLight.HighlightFadeEnd)],
                new Point(0, 0), new Point(1, 0));
            dc.DrawRectangle(top, null, new Rect(radius, 0.25, Size - 2 * radius, 1.5));
            dc.DrawText(text, new Point((Size - text.Width) / 2, (Size - text.Height) / 2));
        }
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private async Task<ImageSource?> LoadAsync(Link link)
    {
        try
        {
            if (link.Icon.Source == IconSourceKind.File && !string.IsNullOrWhiteSpace(link.Icon.Path))
            {
                return await Task.Run(() => LoadImageFile(Environment.ExpandEnvironmentVariables(link.Icon.Path))).ConfigureAwait(false);
            }

            var cached = CachePath(link);
            if (File.Exists(cached))
            {
                return await Task.Run(() => LoadImageFile(cached)).ConfigureAwait(false);
            }

            if (link.Kind == LinkKind.Url)
            {
                return Uri.TryCreate(link.Target, UriKind.Absolute, out var page) ? await LoadFaviconAsync(page, cached).ConfigureAwait(false) : null;
            }

            await _extractors.WaitAsync().ConfigureAwait(false);
            try
            {
                return await Task.Run(() =>
                {
                    var bitmap = IconExtractor.ForLink(link);
                    if (bitmap is null)
                    {
                        return null;
                    }
                    var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.Pixels, bitmap.Width * 4);
                    source.Freeze();
                    SavePng(source, cached);
                    return (ImageSource)source;
                }).ConfigureAwait(false);
            }
            finally
            {
                _extractors.Release();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
        {
            LogLoadFailed(_logger, ex, link.Target);
            Invalidate(link);
            return null;
        }
    }

    private async Task<ImageSource?> LoadFaviconAsync(Uri page, string cachePath)
    {
        var bytes = await _favicons.FetchAsync(page).ConfigureAwait(false);
        if (bytes is null)
        {
            LogNoFavicon(_logger, page.Host);
            return null;
        }
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
            BitmapSource image = frame;
            if (frame.PixelWidth > IconExtractor.LargeSize)
            {
                var scale = (double)IconExtractor.LargeSize / frame.PixelWidth;
                image = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
            }
            var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
            converted.Freeze();
            SavePng(converted, cachePath);
            return (ImageSource)converted;
        }).ConfigureAwait(false);
    }

    private static BitmapFrame LoadImageFile(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        // .ico files hold several sizes; use the largest.
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
        frame.Freeze();
        return frame;
    }

    private void SavePng(BitmapSource source, string path)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex, path);
        }
    }

    private string CachePath(Link link) => Path.Combine(_directory, KeyFor(link) + ".png");

    private static string KeyFor(Link link)
    {
        // Shell icons get a new key when their extraction changes, so pictures cached by an older version are taken
        // again (2: small icons in the faint frame of build 26200). Favicons keep theirs rather than download again.
        var version = link.Kind == LinkKind.Url ? "" : "2|";
        var identity = $"{version}{link.Kind}|{link.Target}|{link.Icon.Source}|{link.Icon.Path}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not load icon for {Target}")]
    private static partial void LogLoadFailed(ILogger logger, Exception ex, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "No favicon found for {Host}; using a letter icon")]
    private static partial void LogNoFavicon(ILogger logger, string host);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not write icon cache file {Path}")]
    private static partial void LogSaveFailed(ILogger logger, Exception ex, string path);
}
