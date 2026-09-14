using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MinkQuickLax.Core.Config;
using MinkQuickLax.Core.Model;

namespace MinkQuickLax.Core.Tests.Config;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly ConfigPaths _paths;

    public ConfigStoreTests()
    {
        _paths = new ConfigPaths(_dir.Path);
    }

    public void Dispose() => _dir.Dispose();

    private ConfigStore NewStore() => new(_paths, _time, NullLogger<ConfigStore>.Instance);

    private static Func<AppConfig, AppConfig> AddLink(string id) =>
        c => c.AddLinks([new Link { Id = id, Name = id, Kind = LinkKind.Url, Target = "https://example.com/" + id }]);

    private void WriteConfig(string text) => File.WriteAllText(_paths.ConfigFile, text);

    private static string ValidJson(params string[] linkIds) =>
        ConfigSerializer.Serialize(new AppConfig { Links = [.. linkIds.Select(id => new Link { Id = id, Name = id, Kind = LinkKind.File, Target = id })] });

    [Fact]
    public void FirstRun_StartsFromDefaults_WithoutWritingAFile()
    {
        using var store = NewStore();

        var result = store.Load();

        Assert.True(result.IsFirstRun);
        Assert.Equal(ConfigSource.Defaults, result.Source);
        Assert.False(result.NeedsUserNotice);
        Assert.False(File.Exists(_paths.ConfigFile));
    }

    [Fact]
    public void Update_SavesAfterTheDelay_AndDebounces()
    {
        using var store = NewStore();
        store.Load();

        store.Update(AddLink("a"));
        _time.Advance(TimeSpan.FromMilliseconds(300));
        store.Update(AddLink("b"));
        _time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.False(File.Exists(_paths.ConfigFile));
        Assert.True(store.HasUnsavedChanges);

        _time.Advance(TimeSpan.FromMilliseconds(1));

        Assert.True(File.Exists(_paths.ConfigFile));
        Assert.False(File.Exists(_paths.TempFile));
        Assert.False(store.HasUnsavedChanges);
        Assert.Equal(["a", "b"], ConfigSerializer.Deserialize(File.ReadAllText(_paths.ConfigFile)).Config.Links.Select(l => l.Id));
    }

    [Fact]
    public void Update_RaisesChangedWithPreviousAndCurrent()
    {
        using var store = NewStore();
        store.Load();
        ConfigChangedEventArgs? seen = null;
        store.Changed += (_, e) => seen = e;

        store.Update(AddLink("a"));

        Assert.NotNull(seen);
        Assert.Empty(seen.Previous.Links);
        Assert.Single(seen.Current.Links);
        Assert.Same(store.Current, seen.Current);
    }

    [Fact]
    public void UpdateReturningSameInstance_DoesNothing()
    {
        using var store = NewStore();
        store.Load();
        var raised = false;
        store.Changed += (_, _) => raised = true;

        store.Update(c => c);

        Assert.False(raised);
        Assert.False(store.HasUnsavedChanges);
    }

    [Fact]
    public void Flush_WritesImmediately()
    {
        using var store = NewStore();
        store.Load();
        store.Update(AddLink("a"));

        store.Flush();

        Assert.True(File.Exists(_paths.ConfigFile));
    }

    [Fact]
    public void ExistingFile_LoadsAndCreatesOneBackup()
    {
        WriteConfig(ValidJson("a"));
        using (var store = NewStore())
        {
            var result = store.Load();
            Assert.Equal(ConfigSource.File, result.Source);
            Assert.Equal(["a"], store.Current.Links.Select(l => l.Id));
        }

        using (var again = NewStore())
        {
            again.Load();
        }

        // Unchanged content is not backed up twice.
        Assert.Single(new BackupManager(_paths.BackupDirectory, _time).List());
    }

    [Fact]
    public void CorruptFile_IsQuarantined_AndNewestReadableBackupRestored()
    {
        var backups = new BackupManager(_paths.BackupDirectory, _time);
        WriteConfig(ValidJson("old"));
        backups.Create(_paths.ConfigFile);
        _time.Advance(TimeSpan.FromMinutes(1));
        WriteConfig(ValidJson("good"));
        backups.Create(_paths.ConfigFile);
        _time.Advance(TimeSpan.FromMinutes(1));
        WriteConfig("{ this is broken");
        backups.Create(_paths.ConfigFile); // newest backup is broken too
        _time.Advance(TimeSpan.FromMinutes(1));
        WriteConfig("{\"links\": [ {\"id\": ");

        using var store = NewStore();
        var result = store.Load();

        Assert.Equal(ConfigSource.Backup, result.Source);
        Assert.True(result.NeedsUserNotice);
        Assert.Equal(["good"], store.Current.Links.Select(l => l.Id));
        Assert.NotNull(result.QuarantinedFile);
        Assert.StartsWith("{\"links\"", File.ReadAllText(result.QuarantinedFile), StringComparison.Ordinal);
        // The restored config is written back as config.json right away.
        Assert.Equal(["good"], ConfigSerializer.Deserialize(File.ReadAllText(_paths.ConfigFile)).Config.Links.Select(l => l.Id));
    }

    [Fact]
    public void CorruptFile_WithoutBackups_StartsFromDefaults()
    {
        WriteConfig("\0\0\0 garbage");

        using var store = NewStore();
        var result = store.Load();

        Assert.Equal(ConfigSource.Defaults, result.Source);
        Assert.False(result.IsFirstRun);
        Assert.True(result.NeedsUserNotice);
        Assert.True(File.Exists(result.QuarantinedFile));
        Assert.True(File.Exists(_paths.ConfigFile));
    }

    [Fact]
    public void RepairedFile_IsSavedBack()
    {
        WriteConfig("""{ "schemaVersion": 1, "links": [], "placements": [ { "id": "p", "refId": "gone" } ] }""");

        using var store = NewStore();
        var result = store.Load();
        store.Flush();

        Assert.Single(result.Repairs);
        Assert.DoesNotContain("gone", File.ReadAllText(_paths.ConfigFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Backups_KeepOnlyTheNewestTen()
    {
        WriteConfig(ValidJson("a"));
        var backups = new BackupManager(_paths.BackupDirectory, _time);

        for (var i = 0; i < 13; i++)
        {
            backups.Create(_paths.ConfigFile);
        }

        var list = backups.List();
        Assert.Equal(BackupManager.Keep, list.Count);
        // All in the same second: the suffixes decide the order.
        Assert.EndsWith("-13.json", list[0].Path, StringComparison.Ordinal);
    }

    [Fact]
    public void SavingAfterADay_BacksUpThePreviousFile()
    {
        using var store = NewStore();
        store.Load();
        store.Update(AddLink("a"));
        store.Flush();
        store.Update(AddLink("b"));
        store.Flush();
        store.Update(AddLink("c"));
        store.Flush();
        // The first overwrite of an existing file makes the first backup; later saves that day do not.
        Assert.Equal(["a"], LinksIn(Assert.Single(store.Backups.List())));

        _time.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        store.Update(AddLink("d"));
        store.Flush();
        _time.Advance(TimeSpan.FromHours(1));
        store.Update(AddLink("e"));
        store.Flush();

        var backups = store.Backups.List();
        Assert.Equal(2, backups.Count);
        Assert.Equal(["a", "b", "c"], LinksIn(backups[0]));
    }

    private static IEnumerable<string> LinksIn(BackupInfo backup) =>
        ConfigSerializer.Deserialize(File.ReadAllText(backup.Path)).Config.Links.Select(l => l.Id);

    [Fact]
    public void SaveFailure_KeepsChangesPendingForTheNextTry()
    {
        using var store = NewStore();
        store.Load();
        store.Update(AddLink("a"));
        using (File.Create(_paths.TempFile))
        {
            // The temp file is locked, so the write fails.
            store.Flush();
            Assert.True(store.HasUnsavedChanges);
        }

        store.Flush();

        Assert.False(store.HasUnsavedChanges);
        Assert.True(File.Exists(_paths.ConfigFile));
    }

    [Fact]
    public void RestoreFrom_BacksUpCurrentFirst()
    {
        using var store = NewStore();
        store.Load();
        store.Update(AddLink("first"));
        var backup = store.BackupNow()!;
        _time.Advance(TimeSpan.FromSeconds(5));
        store.Update(AddLink("second"));

        store.RestoreFrom(backup);

        Assert.Equal(["first"], store.Current.Links.Select(l => l.Id));
        var newest = store.Backups.List()[0];
        Assert.Contains("second", File.ReadAllText(newest.Path), StringComparison.Ordinal);
        Assert.DoesNotContain("second", File.ReadAllText(_paths.ConfigFile), StringComparison.Ordinal);
    }

    [Fact]
    public void ResetToDefaults_KeepsABackup()
    {
        using var store = NewStore();
        store.Load();
        store.Update(AddLink("mine"));

        store.ResetToDefaults();

        Assert.Empty(store.Current.Links);
        Assert.Contains("mine", File.ReadAllText(store.Backups.List()[0].Path), StringComparison.Ordinal);
    }

    [Fact]
    public void NewerSchemaFile_KeepsUnknownFieldsWhenSaved()
    {
        WriteConfig("""{ "schemaVersion": 9, "fromTheFuture": { "x": 1 }, "links": [] }""");

        using var store = NewStore();
        var result = store.Load();
        store.Update(AddLink("a"));
        store.Flush();

        Assert.Equal(9, result.FileSchemaVersion);
        var text = File.ReadAllText(_paths.ConfigFile);
        Assert.Contains("fromTheFuture", text, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 9", text, StringComparison.Ordinal);
    }
}
