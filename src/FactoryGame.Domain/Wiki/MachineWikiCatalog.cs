using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Wiki;

/// <summary>Machine descriptions for generated wiki (KRAVSPEC F22); same data drives API wiki payload.</summary>
public static class MachineWikiCatalog
{
    public static IReadOnlyList<MachineWikiEntry> All { get; } =
    [
        Entry("Boiler", "Raises temperature-related DNA bands using bitwise mask (v1)."),
        Entry("LiquidSeparator", "Splits liquid fraction into two outputs by cut-point rules."),
        Entry("Destilator", "Separation by boiling-point buckets vs DNA."),
        Entry("Mixer", "Mixes two inputs; low intensity = poor/compact DNA, high intensity + processed inputs = volatile DNA for distillation."),
        Entry("Heater", "Increases energy/temperature bits deterministically."),
        Entry("Cooler", "Decreases energy/temperature bits deterministically."),
        Entry("Condenser", "Condenses gas to liquid by lowering boil band (never outputs gas)."),
        Entry("Crystallizer", "Crystallizes spread/unstable liquid to solid via freeze band (never outputs gas)."),
        Entry("Melter", "Melts spread solid to liquid via boil band (compact solids pass through)."),
        Entry("Sorter", "Routes configured elements to ports 1–3; all other to port 4 (rest)."),
        Entry("Tank", "Buffer storage with configurable capacity (small/medium/large)."),
        Entry("Junction", "Splits one input across two outputs with fair alternation or capacity-weighted routing."),
        Entry("RateLimiter", "Caps flow to a configured maximum rate."),
        Entry("SeaportConnector", "Connects factory to seaport pool (in from pipes, out to pipes)."),
        Entry("SeaportIn", "Legacy: pool → factory (out only)."),
        Entry("SeaportOut", "Legacy: factory → pool (in only).")
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
