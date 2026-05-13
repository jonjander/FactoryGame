using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Names;

/// <summary>Deterministic fictional names from DNA; morpheme tables per locale (KRAVSPEC F29).</summary>
public static class ElementNameGenerator
{
    private const int NameVersion = 1;

    public static int Version => NameVersion;

    public static string Generate(long dna, string locale)
    {
        var culture = locale.Equals("sv", StringComparison.OrdinalIgnoreCase) ? "sv" : "en";
        var parts = culture == "sv" ? Swedish : English;
        var u = (ulong)dna;

        var a = (int)(u & 0xFF) % parts.Prefixes.Length;
        var b = (int)((u >> 8) & 0xFF) % parts.Middles.Length;
        var c = (int)((u >> 16) & 0xFF) % parts.Suffixes.Length;
        var d = (int)((u >> 24) & 0xFF) % parts.Tails.Length;

        return string.Concat(parts.Prefixes[a], parts.Middles[b], parts.Suffixes[c], parts.Tails[d]);
    }

    private static readonly MorphemeSet English = new(
        Prefixes: ["Ty", "Bi", "Kar", "Neo", "Xen", "Vol", "Plu", "Zen", "Omni", "Meta"],
        Middles: ["Bo", "Kar", "Nit", "Sul", "Chlor", "Phos", "Bor", "Sil", "Fer", "Cup"],
        Suffixes: ["on", "ate", "ite", "ide", "ium", "al", "yl", "one", "ine", "ose"],
        Tails: ["ium", "ate", "ide", "one", "al", "yl", "ine", "ose", "ite", "ol"]);

    private static readonly MorphemeSet Swedish = new(
        Prefixes: ["Ty", "Bi", "Kar", "Neo", "Xen", "Vol", "Plu", "Zen", "Omni", "Meta"],
        Middles: ["Bo", "Kar", "Nit", "Sul", "Klor", "Fos", "Bor", "Kisel", "Jarn", "Kop"],
        Suffixes: ["on", "at", "it", "id", "ium", "al", "yl", "on", "in", "os"],
        Tails: ["ium", "at", "id", "on", "al", "yl", "in", "os", "it", "ol"]);

    private readonly record struct MorphemeSet(string[] Prefixes, string[] Middles, string[] Suffixes, string[] Tails);
}
