using FactoryGame.Domain.Content;
using FactoryGame.Web.Models;

namespace FactoryGame.Web.Services;

public static class ElementPropertyNarrator
{
    public static string Describe(ElementContentItem element)
    {
        var d = element.Decoded;
        var phase = DescribePhase(d.Phase);
        var risks = DescribeRisks(d.Explosivity, d.Flammability, d.Toxicity);
        var thermal = DescribeThermal(d.BoilingPoint, d.FreezePoint);
        var family = $"It belongs to family {d.FamilyId} in the catalog.";
        var kind = element.Dna == ElementCatalogLookup.CatalogDnaFor(element.Id)
            ? "base element"
            : "material variant";

        return $"{element.Name} ({element.Symbol}) is a {kind} in {phase}. {risks} {thermal} {family}";
    }

    private static string DescribePhase(string phase) =>
        phase switch
        {
            "Liquid" => "liquid phase under normal conditions",
            "Gas" => "gas phase under normal conditions",
            _ => "solid form under normal conditions"
        };

    private static string DescribeRisks(int explosivity, int flammability, int toxicity)
    {
        var parts = new List<string>();
        parts.Add($"Explosivity {explosivity}%, fire risk {flammability}%, and toxicity {toxicity}% (scale 0-100).");

        if (explosivity >= 70 || flammability >= 70)
            parts.Add("Handle with care when heating and mixing.");
        else if (toxicity >= 70)
            parts.Add("Use protection — high toxicity index.");
        else
            parts.Add("Risk profile is moderate for industrial use.");

        return string.Join(" ", parts);
    }

    private static string DescribeThermal(int boiling, int freeze)
    {
        var spread = boiling - freeze;
        if (spread >= 3000)
            return $"Wide temperature span (boil {boiling}, freeze {freeze} on the game scale) makes it flexible in processes.";
        if (spread < 800)
            return $"Narrow temperature span (boil {boiling}, freeze {freeze}) — sensitive to heat and cold.";
        return $"Boiling point {boiling} and freeze point {freeze} on the game temperature scale.";
    }
}
