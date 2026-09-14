using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SpikeLab;

/// <summary>S4: acrylic on a non-layered, topmost, no-activate window.</summary>
internal static class AcrylicSpike
{
    public static async Task<int> RunAsync(string[] args)
    {
        Report.Name = "s4-acrylic";
        const int PatternX = 60, PatternY = 520, PatternW = 1160, PatternH = 360;
        var pattern = PatternWindow(PatternX, PatternY, PatternW, PatternH);
        pattern.Show();
        await Task.Delay(1200);
        using var before = PixelMetrics.Capture(PatternX, PatternY, PatternW, PatternH);

        var modes = new[] { BackdropMode.SystemBackdrop, BackdropMode.SystemBackdropFakeActive, BackdropMode.AccentAcrylic, BackdropMode.Solid };
        var menus = new List<GlassMenu>();
        for (var i = 0; i < modes.Length; i++)
        {
            var m = new GlassMenu(modes[i], modes[i].ToString(), "Open", "Run as administrator", "Open file location", "Remove from screen", "Delete link…")
            {
                Left = PatternX + 40 + i * 280,
                Top = PatternY + 40,
            };
            m.Show();
            if (modes[i] == BackdropMode.SystemBackdropFakeActive)
            {
                m.FakeActivate();
            }
            menus.Add(m);
        }
        await Task.Delay(2000);
        using var after = PixelMetrics.Capture(PatternX, PatternY, PatternW, PatternH);
        after.Save(Path.Combine(Report.ResultsDir(), "s4-acrylic.png"), System.Drawing.Imaging.ImageFormat.Png);

        foreach (var m in menus)
        {
            var r = Native.Rect(m.Handle);
            // An empty strip at the right of the menu (items do not reach there) shows the backdrop.
            var probe = new System.Drawing.Rectangle(r.right - PatternX - 40, r.top - PatternY + 30, 28, r.bottom - r.top - 60);
            var sharpBefore = PixelMetrics.Sharpness(before, probe);
            var sharpAfter = PixelMetrics.Sharpness(after, probe);
            var corner = after.GetPixel(r.left - PatternX + 1, r.top - PatternY + 1);
            var cornerBefore = before.GetPixel(r.left - PatternX + 1, r.top - PatternY + 1);
            var edge = after.GetPixel(r.left - PatternX + 12, r.top - PatternY + 1);
            Report.Line($"{m.Title}: rect={r.right - r.left}x{r.bottom - r.top} sharpness pattern={sharpBefore:F1} through-menu={sharpAfter:F1} ratio={sharpAfter / Math.Max(sharpBefore, 0.01):F2}; corner pixel shows backdrop={corner == cornerBefore} (edge pixel differs={edge != before.GetPixel(r.left - PatternX + 12, r.top - PatternY + 1)})");
            Report.Line($"  exstyle: {Native.ExStyle(m.Handle)}");
        }

        // Focus and interaction with the system-backdrop menu.
        var resultFile = Path.Combine(Path.GetTempPath(), $"spike-focus4-{Environment.ProcessId}.txt");
        File.Delete(resultFile);
        File.Delete(resultFile + ".ready");
        using var target = Process.Start(Environment.ProcessPath!, ["target", resultFile]);
        await Report.WaitUntil(() => File.Exists(resultFile + ".ready"), 15000);
        await Task.Delay(500);
        var ready = File.ReadAllText(resultFile + ".ready").Split(',');
        var targetHwnd = (HWND)nint.Parse(ready[0]);
        await Native.Click(int.Parse(ready[1]), int.Parse(ready[2]));
        Native.TypeText("abc");
        await Task.Delay(300);

        var menu = menus[0];
        var mr = Native.Rect(menu.Handle);
        Native.MouseMove((mr.left + mr.right) / 2, mr.top + 60);
        await Task.Delay(400);
        using (var hover = PixelMetrics.Capture(mr.left - 10, mr.top - 10, mr.right - mr.left + 20, mr.bottom - mr.top + 20))
        {
            hover.Save(Path.Combine(Report.ResultsDir(), "s4-hover.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        await Native.Click((mr.left + mr.right) / 2, mr.top + 60);
        await Task.Delay(400);
        Report.Line($"menu item clicked: {menu.Clicked ?? "(none)"}; menu closed: {!menu.IsVisible}");
        Report.Line($"target still foreground after menu click: {PInvoke.GetForegroundWindow() == targetHwnd}");
        Native.TypeText("def");
        await Task.Delay(300);

        // Click outside closes the menu (menu never has capture or activation, so use Raw Input).
        var second = new GlassMenu(BackdropMode.SystemBackdrop, "outside-click test", "One", "Two") { Left = PatternX + 40, Top = PatternY + 40 };
        second.Show();
        using (var watcher = new RawButtonWatcher())
        {
            watcher.ButtonDown += (x, y) =>
            {
                var r = Native.Rect(second.Handle);
                if (x < r.left || x >= r.right || y < r.top || y >= r.bottom)
                {
                    second.Close();
                }
            };
            await Task.Delay(300);
            await Native.Click(PatternX + 900, PatternY + 300);
            await Task.Delay(400);
        }
        Report.Line($"click outside closed menu: {!second.IsVisible}");
        Report.Line($"target still foreground after outside click on our no-activate window: {PInvoke.GetForegroundWindow() == targetHwnd}");

        PInvoke.PostMessage(targetHwnd, PInvoke.WM_CLOSE, default, default);
        await Report.WaitUntil(() => File.Exists(resultFile), 10000);
        await Task.Delay(200);
        Report.Line($"target result: {File.ReadAllText(resultFile).Trim()} (expect text=abcdef deactivated=0)");

        foreach (var m in menus.Where(m => m.IsVisible))
        {
            m.Close();
        }
        pattern.Close();
        return 0;
    }

    private static Window PatternWindow(int x, int y, int w, int h)
    {
        var checker = new DrawingBrush
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute,
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 8, 8))),
                    new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, 0, 4, 4))),
                    new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(4, 4, 4, 4))),
                },
            },
        };
        var grid = new System.Windows.Controls.Grid { Background = checker };
        var colors = new[] { Colors.OrangeRed, Colors.DeepSkyBlue, Colors.Gold, Colors.MediumSeaGreen };
        for (var i = 0; i < 4; i++)
        {
            grid.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(colors[i]),
                Width = 60,
                Height = h,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(80 + i * 280, 0, 0, 0),
            });
        }
        var win = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            Content = grid,
        };
        win.SourceInitialized += (_, _) => Native.AddExStyles(win, WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
        return win;
    }
}

/// <summary>S7: tray icon survives Explorer restarts; right click opens our own glass menu at the cursor.</summary>
internal static class TraySpike
{
    public static async Task<int> RunAsync(string[] args)
    {
        var pathGuid = args.Contains("pathguid");
        Report.Name = "s7-tray" + (args.Contains("copy") ? "-copy" : "") + (pathGuid ? "-pathguid" : "");
        Report.Line($"exe: {Environment.ProcessPath}");

        var icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/AppIcon.ico")),
            ToolTipText = "SpikeLab tray",
            NoLeftClickDelay = true,
        };
        if (pathGuid)
        {
            icon.Id = H.NotifyIcon.Core.TrayIcon.CreateUniqueGuidForProcessPath(Environment.ProcessPath!);
        }
        GlassMenu? menu = null;
        var watcher = new RawButtonWatcher();
        icon.TrayRightMouseUp += (_, _) =>
        {
            var c = Native.Cursor();
            menu?.Close();
            menu = new GlassMenu(BackdropMode.SystemBackdrop, "SpikeLab", "Add apps…", "Arrange icons", "Settings", "Exit");
            menu.Show();
            var r = Native.Rect(menu.Handle);
            var (w, h) = (r.right - r.left, r.bottom - r.top);
            var work = SystemParameters.WorkArea;
            var x = Math.Clamp(c.X - w, (int)work.Left, (int)work.Right - w);
            var y = Math.Clamp(c.Y - h, (int)work.Top, (int)work.Bottom - h);
            Native.MoveTo(menu.Handle, x, y);
            Report.Line($"right-click at ({c.X},{c.Y}) -> menu at ({x},{y}) size {w}x{h}");
        };
        watcher.ButtonDown += (x, y) =>
        {
            if (menu is { IsVisible: true })
            {
                var r = Native.Rect(menu.Handle);
                if (x < r.left || x >= r.right || y < r.top || y >= r.bottom)
                {
                    menu.Close();
                }
            }
        };

        try
        {
            icon.ForceCreate(enablesEfficiencyMode: false);
            Report.Line("ForceCreate: ok");
        }
        catch (Exception ex)
        {
            Report.Line($"ForceCreate threw: {ex.GetType().Name}: {ex.Message}");
        }
        await Task.Delay(1500);
        Report.Line($"TaskbarIcon.Id={icon.Id} created={icon.IsCreated}");
        Report.Line($"icon registered with shell (GetRect): {IconExists(icon.Id)}");

        if (args.Contains("copy"))
        {
            icon.Dispose();
            watcher.Dispose();
            return 0;
        }

        // Right click through the overflow flyout, like a user would.
        var item = await FindTrayItem("SpikeLab tray");
        if (item is null)
        {
            Report.Line("tray item not found through UI Automation");
        }
        else
        {
            var b = item.Current.BoundingRectangle;
            await Native.Click((int)(b.X + b.Width / 2), (int)(b.Y + b.Height / 2), right: true);
            await Task.Delay(900);
            if (menu is { IsVisible: true })
            {
                var r = Native.Rect(menu.Handle);
                using var shot = PixelMetrics.Capture(Math.Max(0, r.left - 300), Math.Max(0, r.top - 200), 600, 480);
                shot.Save(Path.Combine(Report.ResultsDir(), "s7-menu.png"), System.Drawing.Imaging.ImageFormat.Png);
                var c = Native.Cursor();
                Report.Line($"menu visible; cursor inside or at edge of menu: {c.X >= r.left - 2 && c.X <= r.right + 2 && c.Y >= r.top - 2 && c.Y <= r.bottom + 2}");
                await Native.Click(200, 200);
                await Task.Delay(400);
                Report.Line($"outside click closed tray menu: {!menu.IsVisible}");
            }
            else
            {
                Report.Line("menu did not open");
            }
            Native.Key(Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_ESCAPE);
        }

        // Simulate Explorer restart: remove the icon behind the library's back, then broadcast TaskbarCreated to our windows only.
        var removed = Delete(icon.Id);
        await Task.Delay(500);
        Report.Line($"NIM_DELETE ok={removed}; exists after delete: {IconExists(icon.Id)}");
        var taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
        var ours = OurTopLevelWindows();
        foreach (var hwnd in ours)
        {
            PInvoke.PostMessage(hwnd, taskbarCreated, default, default);
        }
        await Task.Delay(1500);
        Report.Line($"posted TaskbarCreated to {ours.Count} windows; exists again: {IconExists(icon.Id)}");
        icon.Dispose();
        watcher.Dispose();
        await Task.Delay(1000);

        // Same GUID from a different exe path.
        var copyDir = Path.Combine(Path.GetTempPath(), "SpikeLabCopy");
        if (Directory.Exists(copyDir))
        {
            Directory.Delete(copyDir, true);
        }
        CopyDir(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), copyDir);
        var copyArgs = new List<string> { "tray", "copy" };
        if (pathGuid)
        {
            copyArgs.Add("pathguid");
        }
        using (var copy = Process.Start(Path.Combine(copyDir, "SpikeLab.exe"), copyArgs))
        {
            await copy.WaitForExitAsync();
        }
        var copyName = "s7-tray-copy" + (pathGuid ? "-pathguid" : "") + ".log";
        Report.Line($"--- same app from another path ({copyName}):");
        var copyLog = Path.Combine(Path.GetTempPath(), "SpikeLab", copyName);
        foreach (var line in File.Exists(copyLog) ? File.ReadAllLines(copyLog) : ["(no log)"])
        {
            Report.Line("  " + line);
        }
        return 0;
    }

    private static unsafe bool IconExists(Guid id)
    {
        var ident = new NOTIFYICONIDENTIFIER { cbSize = (uint)sizeof(NOTIFYICONIDENTIFIER), guidItem = id };
        return PInvoke.Shell_NotifyIconGetRect(ident, out _).Succeeded;
    }

    private static unsafe bool Delete(Guid id)
    {
        var data = new NOTIFYICONDATAW { cbSize = (uint)sizeof(NOTIFYICONDATAW), uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_GUID, guidItem = id };
        return PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, data);
    }

    private static List<HWND> OurTopLevelWindows()
    {
        var pid = (uint)Environment.ProcessId;
        var list = new List<HWND>();
        PInvoke.EnumWindows((hwnd, _) =>
        {
            unsafe
            {
                uint owner;
                PInvoke.GetWindowThreadProcessId(hwnd, &owner);
                if (owner == pid)
                {
                    list.Add(hwnd);
                }
            }
            return true;
        }, 0);
        return list;
    }

    private static async Task<AutomationElement?> FindTrayItem(string name)
    {
        var root = AutomationElement.RootElement;
        var tray = root.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
        var visible = tray?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        if (visible is not null)
        {
            return visible;
        }
        var chevron = tray?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayIcon"));
        (chevron?.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern)?.Invoke();
        await Task.Delay(1000);
        foreach (AutomationElement w in root.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition))
        {
            if (w.Current.ClassName.Contains("Overflow", StringComparison.Ordinal))
            {
                var found = w.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
                if (found is not null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    private static void CopyDir(string from, string to)
    {
        foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(from, to));
        }
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(from, to), true);
        }
    }
}
