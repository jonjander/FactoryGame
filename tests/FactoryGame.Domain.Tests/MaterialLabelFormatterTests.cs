using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Names;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class MaterialLabelFormatterTests
{
    [Fact]
    public void VariantCode_is_stable_for_same_dna()
    {
        var dna = ElementCatalog.All[2].Dna;
        var a = MaterialLabelFormatter.VariantCode(3, dna);
        var b = MaterialLabelFormatter.VariantCode(3, dna);
        Assert.Equal(a, b);
        Assert.Matches(@"^E03-\d{6}$", a);
    }

    [Fact]
    public void Format_catalog_dna_shows_base_name_only()
    {
        var el = ElementCatalog.All[0];
        var baseName = ElementNameGenerator.Generate(el.Dna, MaterialLabelFormatter.DefaultLocale);
        var label = MaterialLabelFormatter.Format(el.Id, el.Dna);
        Assert.StartsWith(MaterialLabelFormatter.VariantCode(el.Id, el.Dna), label);
        Assert.EndsWith($"({baseName})", label);
    }

    [Fact]
    public void Format_variant_dna_shows_base_and_variant_names()
    {
        const long dnaA = 144964032628459529L;
        const long dnaB = 289768180736920073L;
        var (mixed, _) = DnaTransforms.MixCombined(dnaA, dnaB, 500, 850);

        var label = MaterialLabelFormatter.Format(3, mixed);
        Assert.StartsWith("E03-", label);
        Assert.Contains("(", label);
        Assert.Contains("-", label[(label.IndexOf('(') + 1)..]);
    }
}
