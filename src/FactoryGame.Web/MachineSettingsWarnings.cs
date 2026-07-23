using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;
using FactoryGame.Contracts.Pool;
using FactoryGame.Domain.Simulation;
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
        IReadOnlyDictionary<string, MachineStoreItemDto> machineMeta,
        IReadOnlyList<PoolVariantStackDto>? poolVariants = null)
    {
        if (elementId <= 0)
            return Array.Empty<string>();

        var element = elements.FirstOrDefault(e => e.Id == elementId);
        if (element == null)
            return Array.Empty<string>();

        var warnings = new List<string>();

        if (fieldJsonKey == "outElementId")
        {
            var poolWarning = GetPoolEmptyWarning(machine, elementId, element, poolVariants);
            if (poolWarning != null)
                warnings.Add(poolWarning);
        }

        foreach (var downstream in GetDownstreamMachines(machine, fieldJsonKey, planMachines, connections, machineMeta))
        {
            var reason = MachineInputCompatibility.GetPlayerBlockReason(downstream.Type, element.Dna);
            if (reason == null)
                continue;

            warnings.Add(
                $"This material will block machine {downstream.Id} ({downstream.Type}): {reason}");
        }

        if (fieldJsonKey is "outElementId" && element.Decoded.Phase == "Solid")
        {
            warnings.Add(
                "Solid phase in pool — many process machines (Boiler, Destillator, ...) require liquid or gas. Melt or pick another element.");
        }

        return warnings;
    }

    private static string? GetPoolEmptyWarning(
        MachineDto machine,
        int elementId,
        ElementContentItem element,
        IReadOnlyList<PoolVariantStackDto>? poolVariants)
    {
        if (!IsSeaportOutMachine(machine.Type))
            return null;

        var dna = PlanMachineSettings.GetOutMaterialDna(machine);
        decimal quantity;
        if (poolVariants is { Count: > 0 })
        {
            quantity = poolVariants
                .Where(v => v.ElementId == elementId && (dna == 0 || v.Dna == dna))
                .Sum(v => v.Quantity);
        }
        else
        {
            return null;
        }

        if (quantity > 0)
            return null;

        return dna == 0
            ? $"Pool has none left of {element.Symbol} ({element.Name}) — seaport cannot feed in."
            : $"Pool has none left of the selected variant ({element.Symbol}) — seaport cannot feed in.";
    }

    private static bool IsSeaportOutMachine(string machineType) =>
        machineType.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase);

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
