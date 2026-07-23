using System.Globalization;
using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Names;

/// <summary>
/// Unique material labels: variant code (E03-000652) plus generated base/variant names from DNA.
/// </summary>
public static class MaterialLabelFormatter
{
    public const string DefaultLocale = "en";

    /// <summary>Short stable id, e.g. E03-000652.</summary>
    public static string VariantCode(int elementId, long dna)
    {
        var symbol = ResolveSymbol(elementId);
        return $"{symbol}-{DeriveSuffix(dna)}";
    }

    /// <summary>Full label: E03-000652 (TyBoSodium-VolKarate) or E03-000652 (TyBoSodium) when DNA is catalog.</summary>
    public static string Format(int elementId, long dna, string locale = DefaultLocale)
    {
        var code = VariantCode(elementId, dna);
        var catalogDna = ElementCatalogLookup.CatalogDnaFor(elementId);
        var baseName = ElementNameGenerator.Generate(catalogDna, locale);
        if (dna == catalogDna)
            return $"{code} ({baseName})";

        var variantName = ElementNameGenerator.Generate(dna, locale);
        return $"{code} ({baseName}-{variantName})";
    }

    /// <summary>Parenthetical name part only: (Base) or (Base-Variant).</summary>
    public static string FormatNamePart(int elementId, long dna, string locale = DefaultLocale)
    {
        var catalogDna = ElementCatalogLookup.CatalogDnaFor(elementId);
        var baseName = ElementNameGenerator.Generate(catalogDna, locale);
        if (dna == catalogDna)
            return $"({baseName})";

        var variantName = ElementNameGenerator.Generate(dna, locale);
        return $"({baseName}-{variantName})";
    }

    private static string ResolveSymbol(int elementId)
    {
        var el = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId);
        return el.Id == elementId ? el.Symbol : $"E{elementId:D2}";
    }

    private static string DeriveSuffix(long dna)
    {
        var u = (ulong)dna;
        var mixed = u ^ (u >> 17) ^ (u >> 34);
        var bucket = (int)(mixed % 1_000_000);
        return bucket.ToString("D6", CultureInfo.InvariantCulture);
    }
}
