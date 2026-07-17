using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Names;

/// <summary>Deterministic fictional names from DNA; morpheme tables per locale (KRAVSPEC F29).</summary>
public static class ElementNameGenerator
{
    private const int NameVersion = 1;

    public static int Version => NameVersion;

    public static string Generate(long dna, string locale)
    {
        _ = locale;
        var u = (ulong)dna;

        var a = (int)(u & 0xFF) % English.Prefixes.Length;
        var b = (int)((u >> 8) & 0xFF) % English.Middles.Length;
        var c = (int)((u >> 16) & 0xFF) % English.Suffixes.Length;
        var d = (int)((u >> 24) & 0xFF) % English.Tails.Length;

        return string.Concat(English.Prefixes[a], English.Middles[b], English.Suffixes[c], English.Tails[d]);
    }

    private static readonly MorphemeSet English = new(
        Prefixes: ["Ty", "Bi", "Kar", "Neo", "Xen", "Vol", "Plu", "Zen", "Omni", "Meta"],
        Middles: ["Bo", "Kar", "Nit", "Sul", "Chlor", "Phos", "Bor", "Sil", "Fer", "Cup"],
        Suffixes: ["on", "ate", "ite", "ide", "ium", "al", "yl", "one", "ine", "ose"],
        Tails: ["ium", "ate", "ide", "one", "al", "yl", "ine", "ose", "ite", "ol"]);

    private readonly record struct MorphemeSet(string[] Prefixes, string[] Middles, string[] Suffixes, string[] Tails);
}
