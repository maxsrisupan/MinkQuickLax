using System.Reflection;

namespace MinkQuickLax.Core.Tests.RepositoryRules;

/// <summary>Core must stay free of WPF and Win32 so it can be tested on any machine.</summary>
public sealed class CoreDependencyTests
{
    private static readonly string[] ForbiddenAssemblies =
    [
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Xaml",
        "System.Windows.Forms",
        "Microsoft.Win32.Registry",
        "Microsoft.Win32.SystemEvents",
        "MinkQuickLax.Platform",
    ];

    private static readonly Assembly Core = Assembly.Load("MinkQuickLax.Core");

    [Fact]
    public void Core_DoesNotReferenceWindowsOnlyAssemblies()
    {
        var referenced = Core.GetReferencedAssemblies().Select(name => name.Name);

        Assert.Empty(referenced.Intersect(ForbiddenAssemblies, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Core_DeclaresNoPInvokeMethods()
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        var pinvokes = Core.GetTypes()
            .SelectMany(type => type.GetMethods(All))
            .Where(method => method.Attributes.HasFlag(MethodAttributes.PinvokeImpl))
            .Select(method => $"{method.DeclaringType}.{method.Name}");

        Assert.Empty(pinvokes);
    }
}
