using System.Windows.Media;

namespace MinkQuickLax.Styles;

/// <summary>
/// JetBrains Mono has no Thai letters, and IBM Plex Sans Thai at the same point size looks much smaller next to it, so
/// Thai in HUD and Dot Matrix labels came out tiny. <c>Assets/Fonts/MinkMono.CompositeFont</c> maps the Thai block to
/// IBM Plex Sans Thai at 115% and everything else to JetBrains Mono (SPEC 5.7).
/// </summary>
public static class CompositeFonts
{
    // The composite font only resolves its targets when the family is created with a base URI.
    private static readonly Uri Folder = new("pack://application:,,,/Assets/Fonts/");

    public static FontFamily Mono { get; } = new(Folder, "./#Mink Mono, ./#JetBrains Mono, Cascadia Mono, Consolas");
}
