namespace FactoryGame.Domain.Wiki;

/// <summary>Machine descriptions for generated wiki (KRAVSPEC F22); same data drives API wiki payload.</summary>
public static class MachineWikiCatalog
{
    public static IReadOnlyList<MachineWikiEntry> All { get; } =
    [
        new("Boiler", "1:1", "Raises temperature-related DNA bands using bitwise mask (v1)."),
        new("LiquidSeparator", "1:2", "Splits liquid fraction into two outputs by cut-point rules."),
        new("Destilator", "1:2", "Separation by boiling-point buckets vs DNA."),
        new("Mixer", "2:1", "Mixes two inputs; low intensity = poor/compact DNA, high intensity + processed inputs = volatile DNA for distillation."),
        new("Heater", "1:1", "Increases energy/temperature bits deterministically."),
        new("Cooler", "1:1", "Decreases energy/temperature bits deterministically."),
        new("Condenser", "1:1", "Condenses gas to liquid by lowering boil band (never outputs gas)."),
        new("Crystallizer", "1:1", "Crystallizes spread/unstable liquid to solid via freeze band (never outputs gas)."),
        new("Melter", "1:1", "Melts spread solid to liquid via boil band (compact solids pass through)."),
        new("Sorter", "1:4", "Routes configured elements to ports 1–3; all other to port 4 (rest)."),
        new("SeaportConnector", "1:1", "Kopplar fabrik till seaport-pool (in från rör, ut till rör)."),
        new("SeaportIn", "0:1", "Legacy: pool → fabrik (endast ut)."),
        new("SeaportOut", "1:0", "Legacy: fabrik → pool (endast in).")
    ];

    public readonly record struct MachineWikiEntry(string Type, string Ports, string Summary);
}
