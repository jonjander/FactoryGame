namespace FactoryGame.Domain.Wiki;

/// <summary>Machine descriptions for generated wiki (KRAVSPEC F22); same data drives API wiki payload.</summary>
public static class MachineWikiCatalog
{
    public static IReadOnlyList<MachineWikiEntry> All { get; } =
    [
        new("Boiler", "1:1", "Raises temperature-related DNA bands using bitwise mask (v1)."),
        new("LiquidSeparator", "1:2", "Splits liquid fraction into two outputs by cut-point rules."),
        new("Destilator", "1:2", "Separation by boiling-point buckets vs DNA."),
        new("Mixer", "2:1", "Combines two inputs with ratio settings; bitwise combine of DNA."),
        new("Heater", "1:1", "Increases energy/temperature bits deterministically."),
        new("Cooler", "1:1", "Decreases energy/temperature bits deterministically."),
        new("Sorter", "1:4", "Routes configured elements to ports 1–3; all other to port 4 (rest)."),
        new("SeaportIn", "0:1", "Connector from shared seaport pool into the board (output to pipes)."),
        new("SeaportOut", "1:0", "Connector from the board back into the seaport pool (input from pipes).")
    ];

    public readonly record struct MachineWikiEntry(string Type, string Ports, string Summary);
}
