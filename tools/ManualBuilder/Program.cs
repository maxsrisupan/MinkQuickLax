using ManualBuilder;

// Usage: ManualBuilder --manual <dir> --out <dir> --fonts <dir> [--topics <file.cs>]
var options = new Dictionary<string, string>(StringComparer.Ordinal);
for (var i = 0; i + 1 < args.Length; i += 2)
{
    options[args[i]] = args[i + 1];
}
if (!options.TryGetValue("--manual", out var manual) || !options.TryGetValue("--out", out var output) || !options.TryGetValue("--fonts", out var fonts))
{
    Console.Error.WriteLine("Usage: ManualBuilder --manual <dir> --out <dir> --fonts <dir> [--topics <file.cs>]");
    return 2;
}

var project = ManualProject.Load(Path.GetFullPath(manual), options.GetValueOrDefault("--topics"));
foreach (var diagnostic in project.Diagnostics)
{
    Console.WriteLine(diagnostic);
}
if (project.HasErrors)
{
    return 1;
}

foreach (var file in new ManualWriter(project, fonts).Write(output))
{
    Console.WriteLine($"Manual written: {file}");
}
return 0;
