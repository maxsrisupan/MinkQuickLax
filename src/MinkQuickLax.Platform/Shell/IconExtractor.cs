using System.Runtime.InteropServices;
using MinkQuickLax.Core.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;

namespace MinkQuickLax.Platform.Shell;

/// <summary>Top-down premultiplied BGRA pixels.</summary>
public sealed record IconBitmap(int Width, int Height, byte[] Pixels);

/// <summary>Gets the icon Windows shows for a file, folder or AppsFolder app (spike S5).</summary>
public static unsafe class IconExtractor
{
    public const int LargeSize = 256;

    /// <summary>The shell icon for a link, or null when it has none (URLs get favicons or letters instead).</summary>
    public static IconBitmap? ForLink(Link link, int size = LargeSize)
    {
        var parsingName = link.Kind switch
        {
            LinkKind.ShellApp => @"shell:AppsFolder\" + link.Target,
            LinkKind.App or LinkKind.File or LinkKind.Folder or LinkKind.Command => Environment.ExpandEnvironmentVariables(link.Target),
            _ => null,
        };
        return parsingName is null ? null : ForParsingName(parsingName, size);
    }

    /// <summary>Loads an icon at <paramref name="size"/>. Call off the UI thread; it takes ~60 ms.</summary>
    public static IconBitmap? ForParsingName(string parsingName, int size = LargeSize)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        object factoryObject;
        fixed (char* name = parsingName)
        {
            if (PInvoke.SHCreateItemFromParsingName(name, null, &iid, out factoryObject).Failed)
            {
                return null;
            }
        }
        var factory = (IShellItemImageFactory)factoryObject;
        try
        {
            var bitmap = Render(factory, size);
            // Apps with only small icons come back as a tiny icon inside a framed square; ask for a size they have.
            if (bitmap is not null && size >= 96 && LooksFramed(bitmap))
            {
                bitmap = Render(factory, 48) ?? bitmap;
            }
            return bitmap;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    private static IconBitmap? Render(IShellItemImageFactory factory, int size)
    {
        factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK, out var handle);
        using (handle)
        {
            return Copy((HBITMAP)handle.DangerousGetHandle());
        }
    }

    private static IconBitmap? Copy(HBITMAP hbitmap)
    {
        BITMAP bm;
        if (PInvoke.GetObject(hbitmap, sizeof(BITMAP), &bm) == 0 || bm.bmBits == null || bm.bmBitsPixel != 32)
        {
            return null;
        }
        var width = bm.bmWidth;
        var height = Math.Abs(bm.bmHeight);
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var source = (byte*)bm.bmBits;
        for (var y = 0; y < height; y++)
        {
            // A positive height means the DIB is stored bottom-up.
            var row = bm.bmHeight > 0 ? height - 1 - y : y;
            Marshal.Copy((nint)(source + row * stride), pixels, y * stride, stride);
        }
        return new IconBitmap(width, height, pixels);
    }

    /// <summary>
    /// The shell's frame for small icons: a thin border along every edge with a transparent gap inside, while real
    /// large icons are transparent at the very edge. The border is opaque on older Windows 11 builds but only faintly
    /// visible (alpha 26–77 over 5 px) on build 26200, so any alpha in the outer pixels counts.
    /// </summary>
    internal static bool LooksFramed(IconBitmap bitmap)
    {
        if (bitmap.Width < 64 || bitmap.Height < 64)
        {
            return false;
        }
        const int Rim = 4;
        const int RimAlpha = 16;
        int Alpha(int x, int y) => bitmap.Pixels[(y * bitmap.Width + x) * 4 + 3];
        var w = bitmap.Width;
        var h = bitmap.Height;
        int MaxAlpha(int x, int y, int dx, int dy)
        {
            var max = 0;
            for (var d = 0; d < Rim; d++)
            {
                max = Math.Max(max, Alpha(x + dx * d, y + dy * d));
            }
            return max;
        }
        var edge = 0;
        var samples = 0;
        for (var i = 8; i < w - 8; i += 8)
        {
            samples += 2;
            edge += (MaxAlpha(i, 0, 0, 1) >= RimAlpha ? 1 : 0) + (MaxAlpha(i, h - 1, 0, -1) >= RimAlpha ? 1 : 0);
        }
        for (var i = 8; i < h - 8; i += 8)
        {
            samples += 2;
            edge += (MaxAlpha(0, i, 1, 0) >= RimAlpha ? 1 : 0) + (MaxAlpha(w - 1, i, -1, 0) >= RimAlpha ? 1 : 0);
        }
        if (edge < samples * 0.9)
        {
            return false;
        }
        // Just inside the frame the square is empty.
        var inner = 0;
        var innerSamples = 0;
        for (var i = w / 8; i < w - w / 8; i += 8)
        {
            innerSamples++;
            inner += Alpha(i, h / 8) < 40 ? 1 : 0;
        }
        return inner > innerSamples * 0.8;
    }
}
