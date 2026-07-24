using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Names;
using FactoryGame.Web.Models;

namespace FactoryGame.Web.Services;

public static class ElementContentVariant
{
    public static ElementContentItem WithDna(ElementContentItem baseElement, long? variantDna, string locale = MaterialLabelFormatter.DefaultLocale)
    {
        if (variantDna is not { } dna || dna == 0 || dna == baseElement.Dna)
            return baseElement;

        var decoded = DnaDecoder.Decode(dna);
        var fullLabel = MaterialLabelFormatter.Format(baseElement.Id, dna, locale);

        return new ElementContentItem
        {
            Id = baseElement.Id,
            Dna = dna,
            Symbol = MaterialLabelUi.Compact(fullLabel) ?? MaterialLabelFormatter.VariantCode(baseElement.Id, dna),
            Name = fullLabel,
            Decoded = new ElementDecodedProperties
            {
                Phase = decoded.Phase.ToString(),
                Explosivity = decoded.Explosivity,
                Flammability = decoded.Flammability,
                Toxicity = decoded.Toxicity,
                BoilingPoint = decoded.BoilingPoint,
                FreezePoint = decoded.FreezePoint,
                FamilyId = decoded.FamilyId
            }
        };
    }

    public static bool IsVariant(ElementContentItem element) =>
        element.Dna != ElementCatalogLookup.CatalogDnaFor(element.Id);
}
