using System.Globalization;
using Microsoft.Win32;
using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Layout;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace MinkQuickLax.Platform.Displays;

/// <summary>Connected monitors with stable ids (QueryDisplayConfig device path + EDID), work areas and DPI.</summary>
public sealed unsafe class MonitorProvider : IMonitorProvider
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var targets = ReadTargetsByGdiName();
        var result = new List<MonitorInfo>();
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, (hmon, _, _, _) =>
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            if (!PInvoke.GetMonitorInfo(hmon, (MONITORINFO*)&info))
            {
                return true;
            }
            var gdiName = info.szDevice.ToString();
            var dpi = 96u;
            if (OperatingSystem.IsWindowsVersionAtLeast(8, 1) && PInvoke.GetDpiForMonitor(hmon, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded)
            {
                dpi = dpiX;
            }
            targets.TryGetValue(gdiName, out var target);
            var b = info.monitorInfo.rcMonitor;
            var w = info.monitorInfo.rcWork;
            result.Add(new MonitorInfo(
                string.IsNullOrEmpty(target.DevicePath) ? gdiName : target.DevicePath,
                target.EdidKey,
                new PixelRect(b.left, b.top, b.right, b.bottom),
                new PixelRect(w.left, w.top, w.right, w.bottom),
                (int)dpi,
                (info.monitorInfo.dwFlags & 1 /* MONITORINFOF_PRIMARY */) != 0));
            return true;
        }, 0);
        return result;
    }

    private static Dictionary<string, (string DevicePath, string? EdidKey)> ReadTargetsByGdiName()
    {
        var map = new Dictionary<string, (string, string?)>(StringComparer.OrdinalIgnoreCase);
        if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != WIN32_ERROR.NO_ERROR)
        {
            return map;
        }
        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        fixed (DISPLAYCONFIG_PATH_INFO* pathPtr = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* modePtr = modes)
        {
            if (PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, &pathCount, pathPtr, &modeCount, modePtr, null) != WIN32_ERROR.NO_ERROR)
            {
                return map;
            }
        }

        for (var i = 0; i < pathCount; i++)
        {
            var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
            target.header.adapterId = paths[i].targetInfo.adapterId;
            target.header.id = paths[i].targetInfo.id;
            if (PInvoke.DisplayConfigGetDeviceInfo(&target.header) != 0)
            {
                continue;
            }

            var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            source.header.adapterId = paths[i].sourceInfo.adapterId;
            source.header.id = paths[i].sourceInfo.id;
            if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0)
            {
                continue;
            }

            var devicePath = target.monitorDevicePath.ToString();
            var edid = target.edidManufactureId == 0 && target.edidProductCodeId == 0
                ? null
                : FormattableString.Invariant($"{target.edidManufactureId:X4}-{target.edidProductCodeId:X4}-{EdidSerial(devicePath)}");
            map[source.viewGdiDeviceName.ToString()] = (devicePath, edid);
        }
        return map;
    }

    /// <summary>The EDID serial from the monitor's registry key: the 0xFF text descriptor, else the numeric serial.</summary>
    internal static string EdidSerial(string devicePath)
    {
        // \\?\DISPLAY#BOE0C6B#4&2a892282&0&UID8388688#{e6f07b5f-...}
        var parts = devicePath.Split('#');
        if (parts.Length < 3)
        {
            return "";
        }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters");
            return key?.GetValue("EDID") is byte[] edid ? ParseSerial(edid) : "";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return "";
        }
    }

    internal static string ParseSerial(byte[] edid)
    {
        if (edid.Length < 128)
        {
            return "";
        }
        for (var offset = 54; offset <= 108; offset += 18)
        {
            if (edid[offset] == 0 && edid[offset + 1] == 0 && edid[offset + 3] == 0xFF)
            {
                return System.Text.Encoding.ASCII.GetString(edid, offset + 5, 13).TrimEnd('\n', ' ', '\0');
            }
        }
        var numeric = BitConverter.ToUInt32(edid, 12);
        return numeric == 0 ? "" : numeric.ToString(CultureInfo.InvariantCulture);
    }
}
