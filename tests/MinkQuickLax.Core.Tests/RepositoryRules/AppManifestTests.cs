using System.Xml.Linq;

namespace MinkQuickLax.Core.Tests.RepositoryRules;

/// <summary>Guards the app.manifest settings that the spec depends on.</summary>
public sealed class AppManifestTests
{
    private static readonly XDocument Manifest =
        XDocument.Load(RepoPaths.Combine("src", "MinkQuickLax", "app.manifest"));

    [Fact]
    public void App_NeverRequestsElevation()
    {
        var level = Manifest.Descendants().Single(e => e.Name.LocalName == "requestedExecutionLevel");

        Assert.Equal("asInvoker", (string?)level.Attribute("level"));
        Assert.Equal("false", (string?)level.Attribute("uiAccess"));
    }

    [Fact]
    public void App_IsPerMonitorV2DpiAware()
    {
        var awareness = Manifest.Descendants().Single(e => e.Name.LocalName == "dpiAwareness");

        Assert.Equal("PerMonitorV2", awareness.Value.Trim());
    }
}
