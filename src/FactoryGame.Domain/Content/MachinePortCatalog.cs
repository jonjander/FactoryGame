namespace FactoryGame.Domain.Content;

/// <summary>Canonical port names per machine type for plans and UI (KRAVSPEC-aligned naming).</summary>
public static class MachinePortCatalog
{
    public static bool IsKnownMachineType(string machineType) =>
        PortsByType.ContainsKey(Normalize(machineType));

    public static IReadOnlyList<MachinePort> GetPorts(string machineType)
    {
        var key = Normalize(machineType);
        return PortsByType.TryGetValue(key, out var list)
            ? list
            : Array.Empty<MachinePort>();
    }

    public static IReadOnlyList<MachinePort> GetOutputPorts(string machineType) =>
        GetPorts(machineType).Where(p => p.Direction == PortDirection.Out).ToList();

    public static IReadOnlyList<MachinePort> GetInputPorts(string machineType) =>
        GetPorts(machineType).Where(p => p.Direction == PortDirection.In).ToList();

    private static string Normalize(string machineType) =>
        machineType.Trim();

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<MachinePort>> PortsByType =
        new Dictionary<string, IReadOnlyList<MachinePort>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Boiler"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["LiquidSeparator"] = [new("in", PortDirection.In), new("out1", PortDirection.Out), new("out2", PortDirection.Out)],
            ["Destilator"] = [new("in", PortDirection.In), new("out1", PortDirection.Out), new("out2", PortDirection.Out)],
            ["Mixer"] = [new("in1", PortDirection.In), new("in2", PortDirection.In), new("out", PortDirection.Out)],
            ["Heater"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["Cooler"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["Condenser"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["Crystallizer"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["Melter"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["Sorter"] =
            [
                new("in", PortDirection.In),
                new("out1", PortDirection.Out),
                new("out2", PortDirection.Out),
                new("out3", PortDirection.Out),
                new("out4", PortDirection.Out)
            ],
            ["SeaportConnector"] = [new("in", PortDirection.In), new("out", PortDirection.Out)],
            ["SeaportIn"] = [new("out", PortDirection.Out)],
            ["SeaportOut"] = [new("in", PortDirection.In)]
        };
}

public readonly record struct MachinePort(string Name, PortDirection Direction);

public enum PortDirection
{
    In,
    Out
}
