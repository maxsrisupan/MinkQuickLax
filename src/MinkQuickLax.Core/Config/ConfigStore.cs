using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Config;

public sealed record ConfigPaths(string Directory)
{
    public string ConfigFile => Path.Combine(Directory, "config.json");
    public string TempFile => Path.Combine(Directory, "config.json.tmp");
    public string BackupDirectory => Path.Combine(Directory, "backups");

    /// <summary>%AppData%\MinkQuickLax</summary>
    public static ConfigPaths Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MinkQuickLax"));
}

public enum ConfigSource
{
    /// <summary>Read from config.json.</summary>
    File,

    /// <summary>config.json was unreadable; restored from the newest readable backup.</summary>
    Backup,

    /// <summary>No usable file; started from defaults.</summary>
    Defaults,
}

public sealed record LoadResult(ConfigSource Source, IReadOnlyList<string> Repairs)
{
    /// <summary>No config.json existed before.</summary>
    public bool IsFirstRun { get; init; }

    public BackupInfo? RestoredFrom { get; init; }

    /// <summary>Where the unreadable config.json was moved, if it was.</summary>
    public string? QuarantinedFile { get; init; }

    public int FileSchemaVersion { get; init; } = ConfigMigrator.CurrentVersion;

    /// <summary>The user should be told (restored from backup or reset to defaults after a failure).</summary>
    public bool NeedsUserNotice => Source == ConfigSource.Backup || QuarantinedFile is not null;
}

public sealed class ConfigChangedEventArgs(AppConfig previous, AppConfig current) : EventArgs
{
    public AppConfig Previous { get; } = previous;
    public AppConfig Current { get; } = current;
}

/// <summary>
/// Owns the current <see cref="AppConfig"/>. Changes are saved 500 ms after the last edit by writing a
/// temp file and moving it over config.json, so a crash or power loss leaves either the old or the new file.
/// </summary>
public sealed partial class ConfigStore : IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PeriodicBackupAge = TimeSpan.FromDays(1);

    private readonly ConfigPaths _paths;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly Lock _saveGate = new();
    private readonly ITimer _saveTimer;
    private AppConfig _current = AppConfig.CreateDefault();
    private long _version;
    private long _savedVersion;

    public ConfigStore(ConfigPaths paths, TimeProvider time, ILogger<ConfigStore> logger)
    {
        _paths = paths;
        _time = time;
        _logger = logger;
        Backups = new BackupManager(paths.BackupDirectory, time);
        _saveTimer = time.CreateTimer(_ => SaveNow(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<ConfigChangedEventArgs>? Changed;

    public ConfigPaths Paths => _paths;
    public BackupManager Backups { get; }

    public AppConfig Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool HasUnsavedChanges
    {
        get
        {
            lock (_gate)
            {
                return _version != _savedVersion;
            }
        }
    }

    public LoadResult Load()
    {
        System.IO.Directory.CreateDirectory(_paths.Directory);
        if (!File.Exists(_paths.ConfigFile))
        {
            SetLoaded(AppConfig.CreateDefault(), dirty: false);
            LogStartedFromDefaults(_logger);
            return new LoadResult(ConfigSource.Defaults, []) { IsFirstRun = true };
        }

        try
        {
            var loaded = ReadFile(_paths.ConfigFile);
            var (config, repairs) = ConfigRepair.Repair(loaded.Config);
            LogRepairs(repairs);
            SetLoaded(config, dirty: repairs.Count > 0 || loaded.WasMigrated);
            if (!Backups.NewestMatches(_paths.ConfigFile))
            {
                Backups.Create(_paths.ConfigFile);
            }
            if (loaded.IsNewerThanApp)
            {
                LogNewerSchema(_logger, loaded.FileSchemaVersion, ConfigMigrator.CurrentVersion);
            }
            return new LoadResult(ConfigSource.File, repairs) { FileSchemaVersion = loaded.FileSchemaVersion };
        }
        catch (ConfigFormatException ex)
        {
            LogUnreadable(_logger, ex, _paths.ConfigFile);
        }

        var quarantined = Quarantine(_paths.ConfigFile);
        foreach (var backup in Backups.List())
        {
            try
            {
                var loaded = ReadFile(backup.Path);
                var (config, repairs) = ConfigRepair.Repair(loaded.Config);
                LogRepairs(repairs);
                SetLoaded(config, dirty: true);
                SaveNow();
                LogRestored(_logger, backup.Path);
                return new LoadResult(ConfigSource.Backup, repairs) { RestoredFrom = backup, QuarantinedFile = quarantined };
            }
            catch (ConfigFormatException ex)
            {
                LogUnreadable(_logger, ex, backup.Path);
            }
        }

        SetLoaded(AppConfig.CreateDefault(), dirty: true);
        SaveNow();
        LogStartedFromDefaults(_logger);
        return new LoadResult(ConfigSource.Defaults, []) { QuarantinedFile = quarantined };
    }

    /// <summary>Applies a change and schedules a save. <paramref name="change"/> must not have side effects.</summary>
    public void Update(Func<AppConfig, AppConfig> change)
    {
        AppConfig previous;
        AppConfig next;
        lock (_gate)
        {
            previous = _current;
            next = change(previous);
            if (ReferenceEquals(previous, next))
            {
                return;
            }
            _current = next;
            _version++;
        }
        _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
        Changed?.Invoke(this, new ConfigChangedEventArgs(previous, next));
    }

    /// <summary>Writes pending changes now (call on exit).</summary>
    public void Flush()
    {
        _saveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        SaveNow();
    }

    /// <summary>Saves pending changes, then copies config.json into the backups folder.</summary>
    public BackupInfo? BackupNow()
    {
        Flush();
        if (!File.Exists(_paths.ConfigFile))
        {
            SaveNow(force: true);
        }
        return Backups.Create(_paths.ConfigFile);
    }

    /// <summary>Replaces the current config with a backup; the current file is backed up first.</summary>
    /// <exception cref="ConfigFormatException">The backup is unreadable.</exception>
    public IReadOnlyList<string> RestoreFrom(BackupInfo backup)
    {
        var loaded = ReadFile(backup.Path);
        var (config, repairs) = ConfigRepair.Repair(loaded.Config);
        BackupNow();
        Update(_ => config);
        Flush();
        return repairs;
    }

    /// <summary>Resets everything to defaults; the current file is backed up first.</summary>
    public void ResetToDefaults()
    {
        BackupNow();
        Update(_ => AppConfig.CreateDefault());
        Flush();
    }

    public void Dispose() => _saveTimer.Dispose();

    private static DeserializedConfig ReadFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (IOException ex)
        {
            throw new ConfigFormatException($"Could not read {path}.", ex);
        }
        return ConfigSerializer.Deserialize(text);
    }

    private void SetLoaded(AppConfig config, bool dirty)
    {
        AppConfig previous;
        lock (_gate)
        {
            previous = _current;
            _current = config;
            _version++;
            if (!dirty)
            {
                _savedVersion = _version;
            }
        }
        Changed?.Invoke(this, new ConfigChangedEventArgs(previous, config));
    }

    private void SaveNow(bool force = false)
    {
        lock (_saveGate)
        {
            AppConfig snapshot;
            long version;
            lock (_gate)
            {
                if (!force && _version == _savedVersion)
                {
                    return;
                }
                snapshot = _current;
                version = _version;
            }

            try
            {
                WriteAtomically(snapshot);
                lock (_gate)
                {
                    _savedVersion = Math.Max(_savedVersion, version);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep the version dirty so the next edit or Flush retries.
                LogSaveFailed(_logger, ex, _paths.ConfigFile);
            }
        }
    }

    private void WriteAtomically(AppConfig config)
    {
        System.IO.Directory.CreateDirectory(_paths.Directory);
        var backups = Backups.List();
        var newest = backups.Count > 0 ? backups[0] : null;
        if (File.Exists(_paths.ConfigFile) && (newest is null || _time.GetLocalNow() - newest.CreatedAt > PeriodicBackupAge))
        {
            Backups.Create(_paths.ConfigFile);
        }

        var bytes = Encoding.UTF8.GetBytes(ConfigSerializer.Serialize(config));
        using (var stream = new FileStream(_paths.TempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(_paths.TempFile, _paths.ConfigFile, overwrite: true);
    }

    private string? Quarantine(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        var stamp = _time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(_paths.Directory, $"config.unreadable-{stamp}.json");
        for (var n = 2; File.Exists(target); n++)
        {
            target = Path.Combine(_paths.Directory, $"config.unreadable-{stamp}-{n}.json");
        }
        try
        {
            File.Move(path, target);
            return target;
        }
        catch (IOException ex)
        {
            LogSaveFailed(_logger, ex, target);
            return null;
        }
    }

    private void LogRepairs(IReadOnlyList<string> repairs)
    {
        foreach (var repair in repairs)
        {
            LogRepaired(_logger, repair);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Config repaired: {Repair}")]
    private static partial void LogRepaired(ILogger logger, string repair);

    [LoggerMessage(Level = LogLevel.Error, Message = "Config file is unreadable: {Path}")]
    private static partial void LogUnreadable(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Config restored from backup {Path}")]
    private static partial void LogRestored(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Config started from defaults")]
    private static partial void LogStartedFromDefaults(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Config schema {FileVersion} is newer than this app ({AppVersion}); unknown fields are kept")]
    private static partial void LogNewerSchema(ILogger logger, int fileVersion, int appVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not write {Path}")]
    private static partial void LogSaveFailed(ILogger logger, Exception ex, string path);
}
