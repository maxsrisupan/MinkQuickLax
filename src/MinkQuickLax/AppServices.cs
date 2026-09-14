using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinkQuickLax.Core.Abstractions;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Platform.Displays;
using MinkQuickLax.Platform.Input;
using MinkQuickLax.Platform.SystemIntegration;
using MinkQuickLax.Platform.Windowing;
using MinkQuickLax.Services;
using MinkQuickLax.Surfaces;
using MinkQuickLax.Tray;
using Serilog;

namespace MinkQuickLax;

/// <summary>Composition root: every service is a singleton created on the UI thread.</summary>
public static class AppServices
{
    public static string LogDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinkQuickLax", "logs");

    public static IHost Build(Application application, SingleInstance instance)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddSerilog(Log.Logger, dispose: false);

        builder.Services.AddSingleton(application);
        builder.Services.AddSingleton(instance);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(ConfigPaths.Default());
        builder.Services.AddSingleton<ConfigStore>();
        builder.Services.AddSingleton<IMonitorProvider, MonitorProvider>();
        builder.Services.AddSingleton<SystemEvents>();
        builder.Services.AddSingleton<MouseProximityTracker>();
        builder.Services.AddSingleton<TopmostKeeper>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton(Localizer.Instance);
        builder.Services.AddSingleton<IconCache>();
        builder.Services.AddSingleton<SurfaceHost>();
        builder.Services.AddSingleton<PlacementController>();
        builder.Services.AddSingleton<ArrangeController>();
        builder.Services.AddSingleton<LinkActions>();
        builder.Services.AddSingleton<TrayController>();
        builder.Services.AddSingleton<AppShell>();
        return builder.Build();
    }

    public static Serilog.ILogger CreateLogger() =>
        new LoggerConfiguration()
            // MINKQUICKLAX_DEBUG=1 turns on detailed logs for troubleshooting.
            .MinimumLevel.Is(Environment.GetEnvironmentVariable("MINKQUICKLAX_DEBUG") == "1" ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
            .WriteTo.File(
                Path.Combine(LogDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
}
