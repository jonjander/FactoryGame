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
            return "Boiler requires liquid phase (DNA phase does not match).";

        if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Liquid separator requires liquid phase.";

        if (t.Equals("Destilator", StringComparison.OrdinalIgnoreCase) && d.Phase is MaterialPhase.Solid)
            return "Destilator blocked by solid phase in DNA.";

        if (t.Equals("Condenser", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Gas)
            return "Condenser requires gas phase.";

        if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Crystallizer requires liquid phase.";

        if (t.Equals("Melter", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Solid)
            return "Melter requires solid phase.";

        if (t.Equals("Heater", StringComparison.OrdinalIgnoreCase) && d.Explosivity > 85)
            return "Heater blocked: explosivity in DNA too high.";

        if (t.Equals("Cooler", StringComparison.OrdinalIgnoreCase) && d.Toxicity > 90)
            return "Cooler blocked: toxicity in DNA too high.";

        return null;
    }
}
