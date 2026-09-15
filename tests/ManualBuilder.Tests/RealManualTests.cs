namespace ManualBuilder.Tests;

/// <summary>The manual in the repository builds cleanly and has every section the app opens (SPEC 4.9).</summary>
public sealed class RealManualTests
{
    [Fact]
    public void TheManualHasNoErrors()
    {
        var root = RepoRoot();

        var project = ManualProject.Load(Path.Combine(root, "manual"), Path.Combine(root, "src", "MinkQuickLax", "Manual", "ManualTopics.cs"));

        Assert.False(project.HasErrors, string.Join('\n', project.Diagnostics.Where(d => d.Severity == Severity.Error)));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MinkQuickLax.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("Could not find the repository root.");
    }
}
