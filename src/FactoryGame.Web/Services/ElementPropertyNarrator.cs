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
        var family = $"Det tillhör familj {d.FamilyId} i katalogen — samma DNA ger alltid samma egenskaper i simuleringen.";

        return $"{element.Name} ({element.Symbol}) är ett grundämne i {phase}. {risks} {thermal} {family}";
    }

    private static string DescribePhase(string phase) =>
        phase switch
        {
            "Liquid" => "vätskefas vid normala förhållanden",
            "Gas" => "gasfas vid normala förhållanden",
            _ => "fast form vid normala förhållanden"
        };

    private static string DescribeRisks(int explosivity, int flammability, int toxicity)
    {
        var parts = new List<string>();
        parts.Add($"Explosivitet {explosivity} %, brandrisk {flammability} % och toxicitet {toxicity} % (skala 0–100).");

        if (explosivity >= 70 || flammability >= 70)
            parts.Add("Materialet kräver försiktighet vid uppvärmning och blandning.");
        else if (toxicity >= 70)
            parts.Add("Hantera med skydd — högt giftindex.");
        else
            parts.Add("Riskprofilen är måttlig för industriellt bruk.");

        return string.Join(" ", parts);
    }

    private static string DescribeThermal(int boiling, int freeze)
    {
        var spread = boiling - freeze;
        if (spread >= 3000)
            return $"Stort temperaturspann (kok {boiling}, fryspunkt {freeze} i spelets skala) gör det flexibelt i processer.";
        if (spread < 800)
            return $"Snävt temperaturspann (kok {boiling}, fryspunkt {freeze}) — känsligt för värme och kyla.";
        return $"Kokpunkt {boiling} och fryspunkt {freeze} i spelets temperaturskala.";
    }
}
