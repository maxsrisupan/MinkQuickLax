using System.Globalization;

namespace MinkQuickLax.Core.Model;

/// <summary>What a letter icon shows when a link has no image (SPEC 5.3).</summary>
/// <param name="Text">The first user-perceived character, so Thai vowels and tone marks stay with their consonant.</param>
/// <param name="PaletteIndex">Index into the eight color pairs; stable for the same name.</param>
public sealed record LetterIcon(string Text, int PaletteIndex)
{
    public const int PaletteSize = 8;

    public static LetterIcon For(string? name)
    {
        var trimmed = (name ?? "").Trim();
        var text = trimmed.Length == 0 ? "?" : StringInfo.GetNextTextElement(trimmed).ToUpperInvariant();
        return new LetterIcon(text, (int)(Fnv1a(trimmed) % PaletteSize));
    }

    // FNV-1a over UTF-16 code units: stable across runs and .NET versions, unlike string.GetHashCode.
    private static uint Fnv1a(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }
}
