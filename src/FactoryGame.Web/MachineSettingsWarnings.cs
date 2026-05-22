using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;
using FactoryGame.Web.Models;

namespace FactoryGame.Web;

/// <summary>Warns when a selected element would block downstream machines on the plan.</summary>
public static class MachineSettingsWarnings
{
    public static IReadOnlyList<string> GetElementWarnings(
        MachineDto machine,
        string fieldJsonKey,
        int elementId,
        IReadOnlyList<ElementContentItem> elements,
        IReadOnlyList<MachineDto> planMachines,
        IReadOnlyList<ConnectionDto> connections,
        IReadOnlyDictionary<string, MachineStoreItemDto> machineMeta)
    {
        if (elementId <= 0)
            return Array.Empty<string>();

        var element = elements.FirstOrDefault(e => e.Id == elementId);
        if (element == null)
            return Array.Empty<string>();

        var warnings = new List<string>();

        foreach (var downstream in GetDownstreamMachines(machine, fieldJsonKey, planMachines, connections, machineMeta))
        {
            var reason = GetIncompatibilityReason(downstream.Type, element.Decoded);
            if (reason == null)
                continue;

            warnings.Add(
                $"Detta ämne kommer blockera maskin {downstream.Id} ({downstream.Type}): {reason}");
        }

        if (fieldJsonKey is "outElementId" && element.Decoded.Phase == "Solid")
        {
            warnings.Add(
                "Fast fas i pool — många processmaskiner (Boiler, Destilator, …) kräver vätska eller gas. Smält eller välj annat ämne.");
        }

        return warnings;
    }

    private static string? GetIncompatibilityReason(string machineType, ElementDecodedProperties d)
    {
        var t = machineType.Trim();
        var phase = d.Phase;

        if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase) && phase != "Liquid")
            return "Boiler kräver vätskefas — smält fast material med Melter eller välj annat ämne.";

        if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase) && phase != "Liquid")
            return "Liquid separator kräver vätskefas.";

        if (t.Equals("Destilator", StringComparison.OrdinalIgnoreCase) && phase == "Solid")
            return "Destilator blockeras av fast fas — använd Melter eller välj vätske-/gasämne.";

        if (t.Equals("Condenser", StringComparison.OrdinalIgnoreCase) && phase != "Gas")
            return "Condenser kräver gasfas — värm vätska med Boiler/Heater.";

        if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase) && phase != "Liquid")
            return "Crystallizer kräver vätskefas.";

        if (t.Equals("Melter", StringComparison.OrdinalIgnoreCase) && phase != "Solid")
            return "Melter kräver fast fas.";

        if (t.Equals("Heater", StringComparison.OrdinalIgnoreCase) && d.Explosivity > 85)
            return "Heater blockeras — explosiviteten är för hög. Välj ett mindre explosivt ämne.";

        if (t.Equals("Cooler", StringComparison.OrdinalIgnoreCase) && d.Toxicity > 90)
            return "Cooler blockeras — toxiciteten är för hög.";

        return null;
    }

    private static IEnumerable<MachineDto> GetDownstreamMachines(
        MachineDto source,
        string fieldJsonKey,
        IReadOnlyList<MachineDto> planMachines,
        IReadOnlyList<ConnectionDto> connections,
        IReadOnlyDictionary<string, MachineStoreItemDto> machineMeta)
    {
        var outPorts = ResolveOutPorts(source, fieldJsonKey, machineMeta);
        if (outPorts.Count == 0)
            yield break;

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<(string MachineId, string Port)>();

        foreach (var port in outPorts)
        {
            foreach (var conn in connections.Where(c => c.FromId == source.Id && c.FromPort == port))
                queue.Enqueue((conn.ToId, conn.ToPort));
        }

        while (queue.Count > 0)
        {
            var (machineId, inPort) = queue.Dequeue();
            var key = $"{machineId}\0{inPort}";
            if (!visited.Add(key))
                continue;

            var downstream = planMachines.FirstOrDefault(m => m.Id == machineId);
            if (downstream == null)
                continue;

            yield return downstream;

            if (!machineMeta.TryGetValue(downstream.Type, out var meta))
                continue;

            foreach (var outPort in meta.Ports.Where(p => p.Direction == "out").Select(p => p.Name))
            {
                foreach (var conn in connections.Where(c => c.FromId == machineId && c.FromPort == outPort))
                    queue.Enqueue((conn.ToId, conn.ToPort));
            }
        }
    }

    private static IReadOnlyList<string> ResolveOutPorts(
        MachineDto machine,
        string fieldJsonKey,
        IReadOnlyDictionary<string, MachineStoreItemDto> machineMeta)
    {
        if (fieldJsonKey == "outElementId")
            return machine.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
                   || machine.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase)
                ? ["out"]
                : [];

        if (fieldJsonKey is "port1" or "port2" or "port3")
        {
            return fieldJsonKey switch
            {
                "port1" => ["out1"],
                "port2" => ["out2"],
                "port3" => ["out3"],
                _ => []
            };
        }

        if (!machineMeta.TryGetValue(machine.Type, out var meta))
            return [];

        return meta.Ports.Where(p => p.Direction == "out").Select(p => p.Name).ToList();
    }
}
