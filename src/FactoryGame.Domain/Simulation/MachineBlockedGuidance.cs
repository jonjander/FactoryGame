namespace FactoryGame.Domain.Simulation;

/// <summary>Turns terse DNA block reasons into actionable player guidance (UI + board info).</summary>
public static class MachineBlockedGuidance
{
    public static string Enrich(string machineType, string reason)
    {
        var guidance = GetGuidance(machineType, reason);
        return string.IsNullOrEmpty(guidance) ? reason : $"{reason} {guidance}";
    }

    public static string FormatBlockedIssue(string machineId, string machineType, string blockedReason) =>
        $"Machine {machineId} ({machineType}) is blocked: {Enrich(machineType, blockedReason)}";

    public static string FormatSorterIssue(
        string downstreamId,
        string downstreamType,
        string elementSymbol,
        int elementId,
        string sorterId,
        string reason) =>
        $"Machine {downstreamId} ({downstreamType}) is blocked by element {elementSymbol} (id {elementId}) from sorter {sorterId}: {Enrich(downstreamType, reason)}";

    private static string GetGuidance(string machineType, string reason)
    {
        var t = machineType.Trim();

        if (reason.Contains("solid phase", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Destilator", StringComparison.OrdinalIgnoreCase))
        {
            return "Destilator requires liquid or gas phase — melt solid material with Melter, or choose a different element in the sorter/port.";
        }

        if (reason.Contains("liquid phase", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
                return "Boiler accepts liquids only — melt solid material with Melter or filter out solid phase in the sorter.";
            if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase))
                return "Liquid separator requires liquid — check upstream process (Melter/Condenser).";
            if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase))
                return "Crystallizer requires liquid — condense gas with Condenser or choose a different element.";
        }

        if (reason.Contains("gas phase", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
        {
            return "Condenser requires gas — heat liquid with Boiler/Heater or Destilator (light fraction).";
        }

        if (reason.Contains("solid phase", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Melter", StringComparison.OrdinalIgnoreCase))
        {
            return "Melter requires solid material — crystallize liquid with Crystallizer or choose a different element.";
        }

        if (reason.Contains("explosivity", StringComparison.OrdinalIgnoreCase))
        {
            return "Choose a less explosive element, cool down with Cooler, or disconnect Heater from this stream.";
        }

        if (reason.Contains("toxicity", StringComparison.OrdinalIgnoreCase))
        {
            return "Choose a less toxic element or avoid cooling this material in Cooler.";
        }

        if (reason.Contains("pool volume full", StringComparison.OrdinalIgnoreCase))
        {
            return "The pool is full — sell material on the market or disconnect inflow until volume decreases.";
        }

        return "";
    }
}
