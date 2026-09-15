using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Model;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace MinkQuickLax.Platform.Update;

public enum UpdateOutcome
{
    /// <summary>A development build or a copy that was not installed with the installer cannot update itself.</summary>
    NotInstalled,
    UpToDate,

    /// <summary>A newer version is downloaded and waits to be installed.</summary>
    Ready,
    Failed,
}

public sealed record UpdateResult(UpdateOutcome Outcome, string? Version = null);

/// <summary>
/// Updates from GitHub Releases through Velopack (SPEC 4.11): finds and downloads a newer version quietly, then installs
/// it when the app closes or right away with a restart. Stable releases use Velopack's default channel "win" (so the
/// installer is <c>MinkQuickLax-win-Setup.exe</c>); beta releases are GitHub pre-releases on the "beta" channel.
/// </summary>
public sealed partial class UpdateService(ILogger<UpdateService> logger) : IDisposable
{
    public const string RepositoryUrl = "https://github.com/maxsrisupan/MinkQuickLax";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private UpdateManager? _manager;
    private UpdateChannel _channel;
    private VelopackAsset? _ready;

    public static string ChannelName(UpdateChannel channel) => channel == UpdateChannel.Beta ? "beta" : "win";

    /// <summary>The channel of the installer this copy came from, or null when it was not installed.</summary>
    public static UpdateChannel? InstalledChannel
    {
        get
        {
            try
            {
                return VelopackLocator.Current.Channel switch
                {
                    null => null,
                    "beta" => UpdateChannel.Beta,
                    _ => UpdateChannel.Stable,
                };
            }
            catch (InvalidOperationException)
            {
                // VelopackApp.Build().Run() has not run in this process.
                return null;
            }
        }
    }

    /// <summary>The version that is downloaded and waiting, if any.</summary>
    public string? ReadyVersion => _ready?.Version.ToString();

    public async Task<UpdateResult> CheckAndDownloadAsync(UpdateChannel channel, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manager = Manager(channel);
            if (!manager.IsInstalled)
            {
                return new UpdateResult(UpdateOutcome.NotInstalled);
            }
            var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                if (_ready is { } waiting)
                {
                    return new UpdateResult(UpdateOutcome.Ready, waiting.Version.ToString());
                }
                var current = manager.CurrentVersion?.ToString();
                LogUpToDate(logger, current);
                return new UpdateResult(UpdateOutcome.UpToDate, current);
            }
            var version = info.TargetFullRelease.Version.ToString();
            if (_ready?.Version.ToString() != version)
            {
                await manager.DownloadUpdatesAsync(info, null, cancellationToken).ConfigureAwait(false);
                _ready = info.TargetFullRelease;
                var channelName = ChannelName(channel);
                LogDownloaded(logger, version, channelName);
            }
            return new UpdateResult(UpdateOutcome.Ready, version);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Offline, GitHub rate limits, a release without packages for this channel: try again next time.
            LogCheckFailed(logger, ex);
            return new UpdateResult(UpdateOutcome.Failed);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Installs the waiting version now and starts the app again. The process exits.</summary>
    public void RestartNow()
    {
        if (_manager is { } manager && _ready is { } ready)
        {
            var version = ready.Version.ToString();
            LogApplying(logger, version, true);
            manager.ApplyUpdatesAndRestart(ready);
        }
    }

    /// <summary>Call while the app closes: the installer waits for this process to end, then installs without restarting.</summary>
    public void InstallAfterExit()
    {
        if (_manager is { } manager && _ready is { } ready)
        {
            var version = ready.Version.ToString();
            LogApplying(logger, version, false);
            manager.WaitExitThenApplyUpdates(ready, silent: true, restart: false, []);
        }
    }

    public void Dispose() => _gate.Dispose();

    private UpdateManager Manager(UpdateChannel channel)
    {
        if (_manager is null || _channel != channel)
        {
            var source = new GithubSource(RepositoryUrl, accessToken: null, prerelease: channel == UpdateChannel.Beta);
            _manager = new UpdateManager(source, new UpdateOptions { ExplicitChannel = ChannelName(channel) });
            _channel = channel;
        }
        return _manager;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update {Version} downloaded from channel {Channel}")]
    private static partial void LogDownloaded(ILogger logger, string version, string channel);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check: {Version} is the newest version")]
    private static partial void LogUpToDate(ILogger logger, string? version);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update check failed")]
    private static partial void LogCheckFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing update {Version} (restart now: {Restart})")]
    private static partial void LogApplying(ILogger logger, string version, bool restart);
}
