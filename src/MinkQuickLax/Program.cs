using Microsoft.Extensions.DependencyInjection;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Platform.Interop;
using MinkQuickLax.Platform.SystemIntegration;
using Serilog;

namespace MinkQuickLax;

internal static class Program
{
    private const string InstanceName = "MinkQuickLax";

    [STAThread]
    private static int Main(string[] args)
    {
        // Velopack's hooks go first here once the installer lands (M10).
        using var instance = SingleInstance.Acquire(InstanceName);
        if (!instance.IsFirst)
        {
            // SPEC 7: a second launch asks the running copy to show its settings, then quits.
            instance.Send("show-settings", TimeSpan.FromSeconds(10));
            return 0;
        }

        Log.Logger = AppServices.CreateLogger();
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
        MessageWindow.UnhandledException = ex => Log.Error(ex, "Exception in a window message handler");

        try
        {
            var app = new App();
            app.InitializeComponent();
            app.DispatcherUnhandledException += (_, e) =>
            {
                // Keep the icons on screen; the error is in the log for a bug report.
                Log.Error(e.Exception, "Exception on the UI thread");
                e.Handled = true;
            };

            using var host = AppServices.Build(app, instance);
            var store = host.Services.GetRequiredService<ConfigStore>();
            var load = store.Load();
            var shell = host.Services.GetRequiredService<AppShell>();
            shell.LoadResult = load;

            app.Startup += (_, _) => shell.Start(args.Contains("--startup", StringComparer.OrdinalIgnoreCase));
            app.Exit += (_, _) => shell.Stop();
            return app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
