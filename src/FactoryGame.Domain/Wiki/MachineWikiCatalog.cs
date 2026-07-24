using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Wiki;

/// <summary>Machine descriptions for generated wiki (KRAVSPEC F22); same data drives API wiki payload.</summary>
public static class MachineWikiCatalog
{
    public static IReadOnlyList<MachineWikiEntry> All { get; } =
    [
        Entry("Boiler", "Pressurized liquid boiler — heats batches until they are ready for separation and refining."),
        Entry("LiquidSeparator", "Splits a liquid stream into heavy and light fractions at your cut setting."),
        Entry("Destilator", "Fractionating column — separates vapours by boiling point with reflux control."),
        Entry("Mixer", "Blends two streams. Gentle mixing keeps a stable blend; vigorous mixing wakes volatile fractions for distillation."),
        Entry("GasMixer", "Blends two gas streams into one stable vapour — output stays gas, no volatile wake."),
        Entry("Burner", "Controlled flare for moderately flammable gas — material is consumed completely."),
        Entry("Heater", "Direct heat coils for stepwise warming when you need a nudge, not a full boil."),
        Entry("Cooler", "Heat exchanger that pulls energy from the stream. Heavy toxins can foul the coils."),
        Entry("Condenser", "Chilled coil that condenses gas to liquid — never sends vapour downstream."),
        Entry("Crystallizer", "Supercools unruly liquid until it locks into solid crystal — never outputs gas."),
        Entry("Melter", "Induction furnace for stubborn solids; spread-out feed melts to pourable liquid, compact ingots may pass through."),
        Entry("ToxicMelter", "Splits hazardous fluid and a clean solid carrier into vent gas (out1) and purified liquid (out2). Extreme toxicity can transmute the liquid to the carrier element."),
        Entry("Sorter", "Routes configured elements to ports 1–3; all other to port 4 (rest)."),
        Entry("Tank", "Buffer storage with configurable capacity (small/medium/large)."),
        Entry("Junction", "Splits one input across two outputs with fair alternation or capacity-weighted routing."),
        Entry("RateLimiter", "Caps flow to a configured maximum rate."),
        Entry("SeaportConnector", "Connects factory to seaport pool (in from pipes, out to pipes).")
    ];

    /// <summary>Canonical port names from <see cref="MachinePortCatalog"/> as <c>in[,in2]:out[,out2]</c>.</summary>
    public static string FormatPortsForWiki(string machineType)
    {
        var ports = MachinePortCatalog.GetPorts(machineType);
        if (ports.Count == 0)
            return "?";

        var ins = ports.Where(p => p.Direction == PortDirection.In).Select(p => p.Name);
        var outs = ports.Where(p => p.Direction == PortDirection.Out).Select(p => p.Name);
        return $"{string.Join(',', ins)}:{string.Join(',', outs)}";
    }

    private static MachineWikiEntry Entry(string type, string summary) =>
        new(type, FormatPortsForWiki(type), summary);

    public readonly record struct MachineWikiEntry(string Type, string Ports, string Summary);
}
