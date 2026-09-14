using System.Globalization;

namespace MinkQuickLax.Core.Config;

public sealed record BackupInfo(string Path, DateTimeOffset CreatedAt);

/// <summary>Keeps the newest <see cref="Keep"/> copies of config.json as backups/config-YYYYMMDD-HHmmss.json.</summary>
public sealed class BackupManager(string directory, TimeProvider time)
{
    public const int Keep = 10;
    private const string Prefix = "config-";
    private const string StampFormat = "yyyyMMdd-HHmmss";

    public string Directory { get; } = directory;

    /// <summary>Newest first.</summary>
    public IReadOnlyList<BackupInfo> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }
        return System.IO.Directory.EnumerateFiles(Directory, Prefix + "*.json")
            .Select(path => new BackupInfo(path, StampOf(path)))
            .OrderByDescending(b => b.CreatedAt)
            // Same second: "-10" after "-9" after "-2" after no suffix.
            .ThenByDescending(b => b.Path.Length)
            .ThenByDescending(b => b.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Copies <paramref name="sourceFile"/> into the backup folder. Returns null when the source does not exist.</summary>
    public BackupInfo? Create(string sourceFile)
    {
        if (!File.Exists(sourceFile))
        {
            return null;
        }
        System.IO.Directory.CreateDirectory(Directory);
        var now = time.GetLocalNow();
        var stamp = now.ToString(StampFormat, CultureInfo.InvariantCulture);
        // Several backups in one second get -2, -3…; always count up so a new one never sorts before an older one.
        var sameSecond = System.IO.Directory.EnumerateFiles(Directory, $"{Prefix}{stamp}*.json")
            .Select(p => Path.GetFileNameWithoutExtension(p)[(Prefix.Length + StampFormat.Length)..])
            .Select(suffix => suffix.Length == 0 ? 1 : int.TryParse(suffix.TrimStart('-'), CultureInfo.InvariantCulture, out var n) ? n : 1)
            .DefaultIfEmpty(0)
            .Max();
        var path = Path.Combine(Directory, sameSecond == 0 ? $"{Prefix}{stamp}.json" : $"{Prefix}{stamp}-{sameSecond + 1}.json");
        File.Copy(sourceFile, path);
        Prune();
        return new BackupInfo(path, now);
    }

    /// <summary>True when the newest backup has exactly the same bytes as <paramref name="file"/>.</summary>
    public bool NewestMatches(string file)
    {
        var backups = List();
        var newest = backups.Count > 0 ? backups[0] : null;
        if (newest is null || !File.Exists(file))
        {
            return false;
        }
        return File.ReadAllBytes(newest.Path).AsSpan().SequenceEqual(File.ReadAllBytes(file));
    }

    public void Prune()
    {
        foreach (var old in List().Skip(Keep))
        {
            File.Delete(old.Path);
        }
    }

    private static DateTimeOffset StampOf(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path)[Prefix.Length..];
        var stamp = name.Length >= StampFormat.Length ? name[..StampFormat.Length] : name;
        return DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? new DateTimeOffset(parsed)
            : new DateTimeOffset(File.GetLastWriteTime(path));
    }
}
