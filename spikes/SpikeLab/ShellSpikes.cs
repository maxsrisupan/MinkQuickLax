using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace SpikeLab;

internal sealed record ScannedApp(string Name, string ParsingName, string? Aumid, string? TargetPath)
{
    public bool IsPackaged => ParsingName.Contains('!', StringComparison.Ordinal);
}

/// <summary>S5: enumerate shell:AppsFolder, extract 256px icons, launch a Win32 and a Store app.</summary>
internal static class AppScanSpike
{
    public static async Task<int> RunAsync(string[] args)
    {
        Report.Name = "s5-scan";
        var sw = Stopwatch.StartNew();
        var apps = await Task.Run(Scan);
        Report.Line($"AppsFolder items: {apps.Count} in {sw.ElapsedMilliseconds}ms");
        Report.Line($"packaged (Store/MSIX): {apps.Count(a => a.IsPackaged)}; win32: {apps.Count(a => !a.IsPackaged)}; win32 with real target path: {apps.Count(a => !a.IsPackaged && a.TargetPath is not null)}; win32 exe targets: {apps.Count(a => a.TargetPath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)}");
        foreach (var a in apps.Where(a => !a.IsPackaged && a.TargetPath is null).Take(8))
        {
            Report.Line($"  win32 without target path: {a.Name} | {a.ParsingName}");
        }
        foreach (var a in apps.Where(a => a.TargetPath is not null).Take(5))
        {
            Report.Line($"  with path: {a.Name} | {a.ParsingName} -> {a.TargetPath}");
        }
        foreach (var a in apps.Where(a => a.IsPackaged).Take(3))
        {
            Report.Line($"  packaged: {a.Name} | {a.ParsingName}");
        }
        File.WriteAllLines(Path.Combine(Report.ResultsDir(), "s5-apps.txt"), apps.Select(a => $"{a.Name}\t{a.ParsingName}\t{a.TargetPath}"));

        // Icons at 256px, off the UI thread.
        sw.Restart();
        var icons = await Task.Run(() => apps.Take(48).Select(a => (a.Name, Icon: IconExtractor.Get(a.ParsingName, 256))).ToList());
        Report.Line($"extracted {icons.Count(i => i.Icon is not null)}/{icons.Count} icons at 256px in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / (double)icons.Count:F1}ms each)");
        SaveSheet(icons.Select(i => i.Icon).ToList(), "s5-icons.png");

        // Launch: one Win32 shortcut and one packaged app, both through the AppsFolder parsing name.
        var win32 = apps.FirstOrDefault(a => a.TargetPath?.EndsWith("\\powershell.exe", StringComparison.OrdinalIgnoreCase) == true)
            ?? apps.FirstOrDefault(a => a.TargetPath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
        var store = apps.FirstOrDefault(a => a.ParsingName.StartsWith("Microsoft.WindowsCalculator", StringComparison.Ordinal));
        foreach (var (app, processName) in new[] { (win32, "powershell"), (store, "CalculatorApp") })
        {
            if (app is null)
            {
                continue;
            }
            var before = Process.GetProcessesByName(processName).Select(p => p.Id).ToHashSet();
            var ok = Launcher.OpenShellApp(app.ParsingName);
            await Task.Delay(2500);
            var started = Process.GetProcessesByName(processName).Where(p => !before.Contains(p.Id)).ToList();
            Report.Line($"launch {app.Name} via shell:AppsFolder\\{app.ParsingName}: shellExecute={ok}, new {processName} processes={started.Count}");
            foreach (var p in started)
            {
                try
                {
                    p.Kill();
                }
                catch (Exception ex)
                {
                    Report.Line($"  could not close {p.ProcessName}: {ex.Message}");
                }
            }
        }
        return 0;
    }

    private static unsafe List<ScannedApp> Scan()
    {
        var list = new List<ScannedApp>();
        var folderId = PInvoke.FOLDERID_AppsFolder;
        var itemIid = typeof(IShellItem).GUID;
        PInvoke.SHGetKnownFolderItem(&folderId, KNOWN_FOLDER_FLAG.KF_FLAG_DEFAULT, HANDLE.Null, &itemIid, out var folderObj).ThrowOnFailure();
        var folder = (IShellItem)folderObj;
        var enumIid = typeof(IEnumShellItems).GUID;
        var bhid = PInvoke.BHID_EnumItems;
        folder.BindToHandler(null, &bhid, &enumIid, out var enumObj);
        var items = (IEnumShellItems)enumObj;
        var batch = new IShellItem[1];
        while (true)
        {
            uint fetched = 0;
            items.Next(1, batch, &fetched);
            if (fetched == 0)
            {
                break;
            }
            var item = (IShellItem2)batch[0];
            var name = GetString(item, PInvoke.PKEY_ItemNameDisplay) ?? "";
            PWSTR parsing;
            item.GetDisplayName(SIGDN.SIGDN_PARENTRELATIVEPARSING, &parsing);
            var parsingName = parsing.ToString();
            Marshal.FreeCoTaskMem((nint)parsing.Value);
            var aumid = GetString(item, PInvoke.PKEY_AppUserModel_ID);
            var target = GetString(item, PInvoke.PKEY_Link_TargetParsingPath);
            list.Add(new ScannedApp(name, parsingName, aumid, string.IsNullOrEmpty(target) ? null : target));
            Marshal.ReleaseComObject(item);
        }
        return list.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static unsafe string? GetString(IShellItem2 item, PROPERTYKEY key)
    {
        try
        {
            PWSTR value;
            item.GetString(&key, &value);
            var s = value.ToString();
            Marshal.FreeCoTaskMem((nint)value.Value);
            return s;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static void SaveSheet(List<BitmapSource?> icons, string name)
    {
        const int Cell = 64;
        const int Cols = 12;
        var rows = (icons.Count + Cols - 1) / Cols;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x20, 0x30, 0x3A)), null, new Rect(0, 0, Cols * Cell, rows * Cell + 280));
            for (var i = 0; i < icons.Count; i++)
            {
                if (icons[i] is { } bmp)
                {
                    dc.DrawImage(bmp, new Rect(i % Cols * Cell + 8, i / Cols * Cell + 8, 48, 48));
                }
            }
            // A few at full 256px to judge sharpness.
            var big = icons.Where(i => i is not null).Take(3).ToList();
            for (var i = 0; i < big.Count; i++)
            {
                dc.DrawImage(big[i], new Rect(8 + i * 264, rows * Cell + 16, 256, 256));
            }
        }
        var rtb = new RenderTargetBitmap(Cols * Cell, rows * Cell + 280, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(Report.ResultsDir(), name));
        enc.Save(fs);
    }
}

internal static class IconExtractor
{
    public static unsafe BitmapSource? Get(string appsFolderParsingName, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        HRESULT hr;
        object obj;
        fixed (char* path = "shell:AppsFolder\\" + appsFolderParsingName)
        {
            hr = PInvoke.SHCreateItemFromParsingName(new PCWSTR(path), null, &iid, out obj);
        }
        if (hr.Failed)
        {
            return null;
        }
        var factory = (IShellItemImageFactory)obj;
        try
        {
            factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK, out var hbmp);
            using (hbmp)
            {
                return FromHBitmap((HBITMAP)hbmp.DangerousGetHandle());
            }
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

    /// <summary>Copies a 32bpp DIB section (premultiplied BGRA) into a frozen BitmapSource, keeping alpha.</summary>
    private static unsafe BitmapSource? FromHBitmap(HBITMAP hbmp)
    {
        BITMAP bm;
        if (PInvoke.GetObject(hbmp, sizeof(BITMAP), &bm) == 0 || bm.bmBits == null || bm.bmBitsPixel != 32)
        {
            return null;
        }
        var w = bm.bmWidth;
        var h = Math.Abs(bm.bmHeight);
        var stride = w * 4;
        var pixels = new byte[stride * h];
        var src = (byte*)bm.bmBits;
        for (var y = 0; y < h; y++)
        {
            // GetImage returns a bottom-up DIB when bmHeight is positive.
            var srcRow = bm.bmHeight > 0 ? h - 1 - y : y;
            Marshal.Copy((nint)(src + srcRow * stride), pixels, y * stride, stride);
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }
}

internal static class Launcher
{
    public static unsafe bool OpenShellApp(string parsingName)
    {
        fixed (char* verb = "open")
        fixed (char* file = "shell:AppsFolder\\" + parsingName)
        {
            var info = new SHELLEXECUTEINFOW
            {
                cbSize = (uint)sizeof(SHELLEXECUTEINFOW),
                lpVerb = verb,
                lpFile = file,
                nShow = 1,
                fMask = 0x00000400, // SEE_MASK_FLAG_NO_UI
            };
            return PInvoke.ShellExecuteEx(ref info);
        }
    }
}

/// <summary>S8: WebView2 opens a local HTML file at #id; detect a missing runtime.</summary>
internal static class WebViewSpike
{
    public static async Task<int> RunAsync(string[] args)
    {
        Report.Name = "s8-webview";
        var dir = Path.Combine(Path.GetTempPath(), "SpikeLabManual");
        Directory.CreateDirectory(dir);
        var html = Path.Combine(dir, "manual th.html");
        var body = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"<h2 id=\"section-{i}\">หัวข้อ {i}</h2><p style=\"height:600px\">เนื้อหา {i}</p>"));
        File.WriteAllText(html, $"<!doctype html><meta charset=utf-8><body style='font-family:Segoe UI;background:#10181c;color:#eee'>{body}</body>");

        try
        {
            Report.Line($"runtime version: {Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString()}");
        }
        catch (Exception ex)
        {
            Report.Line($"runtime check threw: {ex.GetType().Name}");
        }
        try
        {
            Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(@"C:\NoSuchWebView2Runtime");
            Report.Line("missing-runtime simulation: no exception (unexpected)");
        }
        catch (Exception ex)
        {
            Report.Line($"missing-runtime simulation threw {ex.GetType().Name} -> fall back to the default browser");
        }

        var web = new Microsoft.Web.WebView2.Wpf.WebView2();
        var window = new Window { Title = "Spike manual", Width = 900, Height = 600, Left = 100, Top = 100, Content = web };
        window.Show();
        var sw = Stopwatch.StartNew();
        var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, Path.Combine(Path.GetTempPath(), "SpikeLabWebView2"));
        await web.EnsureCoreWebView2Async(env);
        Report.Line($"WebView2 ready in {sw.ElapsedMilliseconds}ms");

        var uri = new Uri(html).AbsoluteUri + "#section-30";
        var nav = new TaskCompletionSource<bool>();
        web.CoreWebView2.NavigationCompleted += (_, e) => nav.TrySetResult(e.IsSuccess);
        web.CoreWebView2.Navigate(uri);
        Report.Line($"navigate {uri}: success={await nav.Task}");
        await Task.Delay(800);
        Report.Line($"after load: {await Probe(web, 30)}");

        await web.CoreWebView2.ExecuteScriptAsync("location.hash = '#section-12'");
        await Task.Delay(500);
        Report.Line($"after hash change via script: {await Probe(web, 12)}");
        Screenshot.Save(new Int32Rect(100, 100, 900, 600), "s8-webview.png");

        var mem = Native.Memory();
        Report.Line($"our process private={mem.privateMb:F1}MB (WebView2 runs in separate msedgewebview2 processes)");
        window.Close();
        return 0;
    }

    private static async Task<string> Probe(Microsoft.Web.WebView2.Wpf.WebView2 web, int id) =>
        await web.CoreWebView2.ExecuteScriptAsync($"JSON.stringify({{hash: location.hash, top: Math.round(document.getElementById('section-{id}').getBoundingClientRect().top)}})");
}

/// <summary>S9: stable monitor identity from QueryDisplayConfig, matched to HMONITOR work areas.</summary>
internal static class DisplaySpike
{
    public static Task<int> RunAsync(string[] args)
    {
        Report.Name = "s9-displays";
        var monitors = Read();
        foreach (var m in monitors)
        {
            Report.Line(JsonSerializer.Serialize(m));
        }
        var historyFile = Path.Combine(Report.ResultsDir(), "s9-history.jsonl");
        var previous = File.Exists(historyFile) ? File.ReadAllLines(historyFile).LastOrDefault() : null;
        var current = JsonSerializer.Serialize(monitors.Select(m => new { m.DevicePath, m.EdidKey }));
        Report.Line(previous is null ? "no previous run recorded" : $"same ids as previous run: {previous.EndsWith(current, StringComparison.Ordinal)}");
        File.AppendAllLines(historyFile, [$"{DateTime.Now:O}\t{Environment.TickCount64 / 1000}s-uptime\t{current}"]);
        return Task.FromResult(0);
    }

    internal sealed record MonitorId(string GdiName, string DevicePath, string FriendlyName, ushort EdidManufacturer, ushort EdidProduct, uint ConnectorInstance, string? EdidSerial, string EdidKey, string Work, string Bounds, uint Dpi, bool Primary);

    private static unsafe List<MonitorId> Read()
    {
        if (PInvoke.GetDisplayConfigBufferSizes(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != WIN32_ERROR.NO_ERROR) throw new InvalidOperationException("GetDisplayConfigBufferSizes");
        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        fixed (DISPLAYCONFIG_PATH_INFO* pp = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* mp = modes)
        {
            if (PInvoke.QueryDisplayConfig(QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, &pathCount, pp, &modeCount, mp, null) != WIN32_ERROR.NO_ERROR) throw new InvalidOperationException("QueryDisplayConfig");
        }

        var byGdi = new Dictionary<string, (string Path, string Friendly, ushort Mfg, ushort Product, uint Connector)>();
        for (var i = 0; i < pathCount; i++)
        {
            var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
            target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
            target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
            target.header.adapterId = paths[i].targetInfo.adapterId;
            target.header.id = paths[i].targetInfo.id;
            PInvoke.DisplayConfigGetDeviceInfo(&target.header);

            var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            source.header.adapterId = paths[i].sourceInfo.adapterId;
            source.header.id = paths[i].sourceInfo.id;
            PInvoke.DisplayConfigGetDeviceInfo(&source.header);

            byGdi[source.viewGdiDeviceName.ToString()] = (target.monitorDevicePath.ToString(), target.monitorFriendlyDeviceName.ToString(),
                target.edidManufactureId, target.edidProductCodeId, target.connectorInstance);
        }

        var result = new List<MonitorId>();
        PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, (hmon, _, _, _) =>
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            PInvoke.GetMonitorInfo(hmon, (MONITORINFO*)&info);
            var gdi = info.szDevice.ToString();
            PInvoke.GetDpiForMonitor(hmon, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
            byGdi.TryGetValue(gdi, out var t);
            var serial = t.Path is null ? null : EdidSerial(t.Path);
            var w = info.monitorInfo.rcWork;
            var b = info.monitorInfo.rcMonitor;
            result.Add(new MonitorId(gdi, t.Path ?? "", t.Friendly ?? "", t.Mfg, t.Product, t.Connector, serial,
                $"{t.Mfg:X4}-{t.Product:X4}-{serial}", $"{w.left},{w.top},{w.right},{w.bottom}", $"{b.left},{b.top},{b.right},{b.bottom}", dpiX,
                (info.monitorInfo.dwFlags & 1) != 0));
            return true;
        }, 0);
        return result;
    }

    /// <summary>Reads the EDID serial (numeric, or the 0xFF text descriptor) from the monitor's registry key.</summary>
    private static string? EdidSerial(string devicePath)
    {
        // \\?\DISPLAY#SHP14D0#4&1b3...&UID8388688#{e6f07b5f-...}
        var parts = devicePath.Split('#');
        if (parts.Length < 3)
        {
            return null;
        }
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters");
        if (key?.GetValue("EDID") is not byte[] edid || edid.Length < 128)
        {
            return null;
        }
        for (var offset = 54; offset <= 108; offset += 18)
        {
            if (edid[offset] == 0 && edid[offset + 1] == 0 && edid[offset + 3] == 0xFF)
            {
                return System.Text.Encoding.ASCII.GetString(edid, offset + 5, 13).Trim('\n', ' ', '\0');
            }
        }
        var numeric = BitConverter.ToUInt32(edid, 12);
        return numeric == 0 ? null : numeric.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
