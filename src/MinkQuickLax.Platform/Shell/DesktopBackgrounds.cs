using System.Runtime.InteropServices;
using Microsoft.Win32;
using MinkQuickLax.Core.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace MinkQuickLax.Platform.Shell;

/// <summary>Reads each monitor's desktop background through <c>IDesktopWallpaper</c>, for the Glass frost (SPEC 5.5).</summary>
public static unsafe class DesktopBackgrounds
{
    /// <summary>Monitors that are attached, with their pictures. Empty when Windows cannot tell. Call on an STA thread.</summary>
    public static IReadOnlyList<DesktopBackground> Read()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            return [];
        }
        IDesktopWallpaper? wallpaper = null;
        try
        {
            wallpaper = (IDesktopWallpaper)new DesktopWallpaper();
            wallpaper.GetPosition(out var position);
            wallpaper.GetBackgroundColor(out var color);
            var rgb = (int)(((color.Value & 0xFF) << 16) | (color.Value & 0xFF00) | ((color.Value >> 16) & 0xFF));
            var fit = Enum.IsDefined((WallpaperFit)(int)position) ? (WallpaperFit)(int)position : WallpaperFit.Fill;
            wallpaper.GetMonitorDevicePathCount(out var count);
            var result = new List<DesktopBackground>();
            for (var i = 0u; i < count; i++)
            {
                wallpaper.GetMonitorDevicePathAt(i, out var idPointer);
                var id = TakeString(idPointer);
                // Detached monitors that still have a picture assigned report an empty rectangle.
                wallpaper.GetMonitorRECT(id, out var r);
                if (r.right <= r.left || r.bottom <= r.top)
                {
                    continue;
                }
                wallpaper.GetWallpaper(id, out var pathPointer);
                result.Add(new DesktopBackground(new PixelRect(r.left, r.top, r.right, r.bottom), ImageFile(TakeString(pathPointer)), fit, rgb));
            }
            return result;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return [];
        }
        finally
        {
            if (wallpaper is not null)
            {
                Marshal.ReleaseComObject(wallpaper);
            }
        }
    }

    /// <summary>
    /// The picture file. A slideshow or Spotlight has no path per monitor; then the picture on screen is the one in the
    /// registry (Windows keeps it as TranscodedWallpaper). Both empty means a solid color.
    /// </summary>
    private static string? ImageFile(string path)
    {
        if (path.Length > 0 && File.Exists(path))
        {
            return path;
        }
        try
        {
            using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            return desktop?.GetValue("WallPaper") is string current && current.Length > 0 && File.Exists(current) ? current : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string TakeString(PWSTR value)
    {
        if (value.Value is null)
        {
            return "";
        }
        var text = value.ToString();
        PInvoke.CoTaskMemFree(value.Value);
        return text;
    }
}
