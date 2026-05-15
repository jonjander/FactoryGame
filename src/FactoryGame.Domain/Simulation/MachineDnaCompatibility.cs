using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

/// <summary>MVP rules: incompatible DNA vs machine type (expand with KRAVSPEC machine bands).</summary>
public static class MachineDnaCompatibility
{
    public static string? GetIncompatibilityReason(string machineType, long dna)
    {
        var d = DnaDecoder.Decode(dna);
        var t = machineType.Trim();

        if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Boiler kräver vätskefas (DNA-fas matchar inte).";

        if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Liquid separator kräver vätskefas.";

        if (t.Equals("Destilator", StringComparison.OrdinalIgnoreCase) && d.Phase is MaterialPhase.Solid)
            return "Destilator blockeras av fast fas i DNA.";

        if (t.Equals("Condenser", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Gas)
            return "Condenser kräver gasfas.";

        if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Crystallizer kräver vätskefas.";

        if (t.Equals("Melter", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Solid)
            return "Melter kräver fast fas.";

        if (t.Equals("Heater", StringComparison.OrdinalIgnoreCase) && d.Explosivity > 85)
            return "Heater blockeras: explosivitet i DNA för hög.";

        if (t.Equals("Cooler", StringComparison.OrdinalIgnoreCase) && d.Toxicity > 90)
            return "Cooler blockeras: toxicitet i DNA för hög.";

        return null;
    }
}
