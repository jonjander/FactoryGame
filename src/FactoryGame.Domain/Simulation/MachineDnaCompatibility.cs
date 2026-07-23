using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

/// <summary>MVP rules: incompatible DNA vs machine type (expand with KRAVSPEC machine bands).</summary>
public static class MachineDnaCompatibility
{
    public static string? GetIncompatibilityReason(string machineType, long dna)
    {
        var reason = MachineInputCompatibility.GetPlayerBlockReason(machineType, dna);
        if (reason == null)
            return null;

        var label = machineType.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase)
            ? "Liquid separator"
            : machineType;

        return $"{label}: {reason}";
    }
}
