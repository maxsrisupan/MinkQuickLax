using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SpikeLab;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        if (mode == "drive")
        {
            return MouseDriver.Run(args.Skip(1).ToArray());
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var exitCode = 0;
        app.Startup += async (_, _) =>
        {
            try
            {
                exitCode = mode switch
                {
                    "target" => await FocusTarget.RunAsync(args.Skip(1).ToArray()),
                    "icons" => await IconWindowsSpike.RunAsync(args.Skip(1).ToArray()),
                    "acrylic" => await AcrylicSpike.RunAsync(args.Skip(1).ToArray()),
                    "tray" => await TraySpike.RunAsync(args.Skip(1).ToArray()),
                    "scan" => await AppScanSpike.RunAsync(args.Skip(1).ToArray()),
                    "webview" => await WebViewSpike.RunAsync(args.Skip(1).ToArray()),
                    "displays" => await DisplaySpike.RunAsync(args.Skip(1).ToArray()),
                    _ => 2,
                };
            }
            catch (Exception ex)
            {
                Report.Line($"UNHANDLED: {ex}");
                exitCode = 1;
            }
            finally
            {
                Report.Flush();
                app.Shutdown(exitCode);
            }
        };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Report.Line($"DISPATCHER UNHANDLED: {e.Exception}");
            e.Handled = true;
        };
        return app.Run();
    }
}

/// <summary>Collects result lines and writes them next to the repo's spikes/results folder.</summary>
internal static class Report
{
    private static readonly List<string> Lines = [];
    public static string Name { get; set; } = "spike";

    public static void Line(string text)
    {
        lock (Lines)
        {
            Lines.Add($"[{DateTime.Now:HH:mm:ss.fff}] {text}");
        }
    }

    public static void Flush()
    {
        var dir = ResultsDir();
        Directory.CreateDirectory(dir);
        lock (Lines)
        {
            File.WriteAllLines(Path.Combine(dir, Name + ".log"), Lines);
        }
    }

    public static string ResultsDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "MinkQuickLax.slnx")))
            {
                return Path.Combine(d.FullName, "spikes", "results");
            }
        }
        return Path.Combine(Path.GetTempPath(), "SpikeLab");
    }

    public static Task Delay(int ms) => Task.Delay(ms);

    public static async Task WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(50);
        }
    }

    public static void DoEvents() =>
        Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
}
