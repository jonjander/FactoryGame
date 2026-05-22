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
        $"Maskin {machineId} ({machineType}) är blockerad: {Enrich(machineType, blockedReason)}";

    public static string FormatSorterIssue(
        string downstreamId,
        string downstreamType,
        string elementSymbol,
        int elementId,
        string sorterId,
        string reason) =>
        $"Maskin {downstreamId} ({downstreamType}) blockeras av element {elementSymbol} (id {elementId}) från sorter {sorterId}: {Enrich(downstreamType, reason)}";

    private static string GetGuidance(string machineType, string reason)
    {
        var t = machineType.Trim();

        if (reason.Contains("fast fas", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Destilator", StringComparison.OrdinalIgnoreCase))
        {
            return "Destilator kräver vätske- eller gasfas — smält fast material med Melter, eller välj ett annat grundämne i sorter/port.";
        }

        if (reason.Contains("vätskefas", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
                return "Boiler tar bara vätskor — smält fast material med Melter eller filtrera bort fast fas i sorter.";
            if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase))
                return "Liquid separator kräver vätska — kontrollera upstream-process (Melter/Condenser).";
            if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase))
                return "Crystallizer kräver vätska — kondensera gas med Condenser eller välj annat ämne.";
        }

        if (reason.Contains("gasfas", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
        {
            return "Condenser kräver gas — värm vätska med Boiler/Heater eller Destilator (lätt fraktion).";
        }

        if (reason.Contains("fast fas", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Melter", StringComparison.OrdinalIgnoreCase))
        {
            return "Melter kräver fast material — kristallisera vätska med Crystallizer eller välj annat ämne.";
        }

        if (reason.Contains("explosivitet", StringComparison.OrdinalIgnoreCase))
        {
            return "Välj ett mindre explosivt grundämne, kyl ned med Cooler, eller koppla bort Heater från denna ström.";
        }

        if (reason.Contains("toxicitet", StringComparison.OrdinalIgnoreCase))
        {
            return "Välj ett mindre giftigt grundämne eller undvik att kyla detta material i Cooler.";
        }

        if (reason.Contains("pool volume full", StringComparison.OrdinalIgnoreCase))
        {
            return "Poolen är full — sälj material på börsen eller koppla bort inflödet tills volymen minskar.";
        }

        return "";
    }
}
