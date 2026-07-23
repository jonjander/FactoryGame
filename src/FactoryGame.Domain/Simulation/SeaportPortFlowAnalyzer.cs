using System.Text.Json;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Names;
using FactoryGame.Domain.Simulation.Processors;

namespace FactoryGame.Domain.Simulation;

/// <summary>Per-port seaport flow hints for UI (what feeds seaport.in, what pool element leaves seaport.out).</summary>
public static class SeaportPortFlowAnalyzer
{
    public static IReadOnlyList<SeaportPortFlowDetail> AnalyzePorts(
        IReadOnlyList<MachineInfo> machines,
        IReadOnlyList<ConnectionInfo> connections,
        bool isRunning,
        BoardLineState? runtime,
        SeaportTickDelta? lastDelta)
    {
        var connFrom = connections.GroupBy(c => (c.FromId, c.FromPort))
            .ToDictionary(g => g.Key, g => g.First());
        var connTo = connections.GroupBy(c => (c.ToId, c.ToPort))
            .ToDictionary(g => g.Key, g => g.First());
        var machineById = machines.ToDictionary(m => m.Id, StringComparer.Ordinal);

        var results = new List<SeaportPortFlowDetail>();
        foreach (var m in machines)
        {
            if (m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
            {
                AddConnectorPort(results, m, "in", "in", connTo, machineById, connections, runtime, lastDelta, isRunning);
                AddConnectorPort(results, m, "out", "out", connFrom, machineById, connections, runtime, lastDelta, isRunning);
            }
        }

        return results;
    }

    private static void AddConnectorPort(
        List<SeaportPortFlowDetail> results,
        MachineInfo m,
        string portName,
        string direction,
        IReadOnlyDictionary<(string, string), ConnectionInfo> portLinks,
        IReadOnlyDictionary<string, MachineInfo> machineById,
        IReadOnlyList<ConnectionInfo> connections,
        BoardLineState? runtime,
        SeaportTickDelta? lastDelta,
        bool isRunning)
    {
        var isInPort = direction == "in";
        portLinks.TryGetValue((m.Id, portName), out var link);

        var connected = link != null;
        string? linkedMachineId = null;
        string? linkedPort = null;
        if (link != null)
        {
            if (isInPort)
            {
                linkedMachineId = link.FromId;
                linkedPort = link.FromPort;
            }
            else
            {
                linkedMachineId = link.ToId;
                linkedPort = link.ToPort;
            }
        }

        int? elementId = null;
        string? elementSymbol = null;
        string summary;
        var isEstimate = !isRunning || runtime == null;

        if (!connected)
        {
            summary = isInPort
                ? "Input «in» not connected — no material from factory to pool."
                : "Output «out» not connected — no material from pool to factory.";
        }
        else if (isInPort)
        {
            var pkt = TryReadPacket(runtime, m.Id, portName, isOutput: false)
                      ?? (linkedMachineId != null && linkedPort != null
                          ? TryReadPacket(runtime, linkedMachineId, linkedPort, isOutput: true)
                          : null);
            if (pkt != null)
            {
                elementId = pkt.ElementId;
                elementSymbol = MaterialLabelFormatter.Format(pkt.ElementId, pkt.Dna);
            }
            else if (isRunning && lastDelta != null && lastDelta.DepositedToPool.Count > 0)
            {
                var top = lastDelta.DepositedToPool.OrderByDescending(kv => kv.Value).First();
                elementId = top.Key;
                var dna = ElementCatalogLookup.CatalogDnaFor(top.Key);
                elementSymbol = MaterialLabelFormatter.Format(top.Key, dna);
            }
            else if (linkedMachineId != null && linkedPort != null)
            {
                var traced = TraceUpstreamMaterial(
                    linkedMachineId, linkedPort, machineById, connections, runtime, new HashSet<string>(StringComparer.Ordinal));
                elementId = traced.ElementId;
                elementSymbol = traced.ElementSymbol;
            }

            var depositPhase = pkt != null
                ? MaterialPhaseLabels.PhaseLabel(MaterialPhaseLabels.DecodePhase(pkt.Dna))
                : ResolvePredictedDepositPhase(linkedMachineId, linkedPort, machineById, connections, runtime);
            var path = FormatPath(linkedMachineId, linkedPort, m.Id, portName);
            summary = elementSymbol != null
                ? depositPhase != null
                    ? $"To pool: {elementSymbol} ({depositPhase}) via {path}"
                    : $"To pool: {elementSymbol} via {path}"
                : $"To pool via {path} (element not predetermined)";
        }
        else
        {
            var poolElementId = SeaportConnectorProcessor.ParseOutElementId(m.Settings?.GetRawText());
            elementId = poolElementId > 0 ? poolElementId : null;
            if (elementId is > 0)
            {
                var materialDna = SeaportConnectorProcessor.ResolveOutMaterialDna(
                    m.Settings?.GetRawText(), elementId.Value);
                elementSymbol = MaterialLabelFormatter.Format(elementId.Value, materialDna);
            }

            if (isRunning && lastDelta != null && lastDelta.WithdrawnFromPool.Count > 0)
            {
                var top = lastDelta.WithdrawnFromPool.OrderByDescending(kv => kv.Value).First();
                elementId = top.Key;
                var dna = ElementCatalogLookup.CatalogDnaFor(top.Key);
                elementSymbol = MaterialLabelFormatter.Format(top.Key, dna);
            }

            var pkt = TryReadPacket(runtime, m.Id, portName, isOutput: true);
            if (pkt != null)
            {
                elementId = pkt.ElementId;
                elementSymbol = MaterialLabelFormatter.Format(pkt.ElementId, pkt.Dna);
            }

            var path = FormatPath(m.Id, portName, linkedMachineId, linkedPort);
            summary = elementSymbol != null
                ? $"From pool: {elementSymbol} → {path}"
                : $"From pool → {path}";
        }

        results.Add(new SeaportPortFlowDetail(
            m.Id,
            m.Type,
            portName,
            direction,
            connected,
            linkedMachineId,
            linkedPort,
            elementId,
            elementSymbol,
            summary,
            isEstimate));
    }

    private static MaterialPacket? TryReadPacket(BoardLineState? runtime, string machineId, string port, bool isOutput)
    {
        if (runtime == null || !runtime.Machines.TryGetValue(machineId, out var machine))
            return null;

        var buffers = isOutput ? machine.OutputPorts : machine.InputPorts;
        if (!buffers.TryGetValue(port, out var buffer))
            return null;

        return buffer.Snapshot().FirstOrDefault();
    }

    private static (int? ElementId, string? ElementSymbol) TraceUpstreamMaterial(
        string machineId,
        string portName,
        IReadOnlyDictionary<string, MachineInfo> machineById,
        IReadOnlyList<ConnectionInfo> connections,
        BoardLineState? runtime,
        HashSet<string> visited)
    {
        var key = $"{machineId}\0{portName}";
        if (!visited.Add(key))
            return (null, null);

        var pkt = TryReadPacket(runtime, machineId, portName, isOutput: true)
                  ?? TryReadPacket(runtime, machineId, portName, isOutput: false);
        if (pkt != null)
            return (pkt.ElementId, MaterialLabelFormatter.Format(pkt.ElementId, pkt.Dna));

        if (!machineById.TryGetValue(machineId, out var machine))
            return (null, null);

        var settings = machine.Settings?.GetRawText();

        if (machine.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
            && portName.Equals("out", StringComparison.OrdinalIgnoreCase))
        {
            var id = SeaportConnectorProcessor.ParseOutElementId(settings);
            if (id <= 0)
                return (null, null);
            var outDna = SeaportConnectorProcessor.ResolveOutMaterialDna(settings, id);
            return (id, MaterialLabelFormatter.Format(id, outDna));
        }

        if (machine.Type.Equals("Sorter", StringComparison.OrdinalIgnoreCase)
            && portName.StartsWith("out", StringComparison.OrdinalIgnoreCase))
        {
            var ids = ParseSorterOutElements(settings, portName);
            if (ids.Count == 1)
            {
                var catalogDna = ElementCatalogLookup.CatalogDnaFor(ids[0]);
                return (ids[0], MaterialLabelFormatter.Format(ids[0], catalogDna));
            }
            return (null, null);
        }

        var inPort = ResolveUpstreamInputPort(machine.Type, portName);
        if (inPort == null)
            return (null, null);

        var connTo = connections.FirstOrDefault(c => c.ToId == machineId && c.ToPort == inPort);
        if (connTo == null)
            return (null, null);

        var traced = TraceUpstreamMaterial(connTo.FromId, connTo.FromPort, machineById, connections, runtime, visited);
        if (traced.ElementId == null)
            return traced;

        var predictedDna = ElementCatalog.All.FirstOrDefault(e => e.Id == traced.ElementId.Value).Dna;
        if (machine.Type.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
            predictedDna = DnaTransforms.Heat(predictedDna);
        else if (machine.Type.Equals("Heater", StringComparison.OrdinalIgnoreCase))
            predictedDna = DnaTransforms.Heat(predictedDna, 4);
        else if (machine.Type.Equals("Cooler", StringComparison.OrdinalIgnoreCase))
            predictedDna = DnaTransforms.Cool(predictedDna);

        if (traced.ElementId is > 0)
            return (traced.ElementId, MaterialLabelFormatter.Format(traced.ElementId.Value, predictedDna));

        var match = ElementCatalog.All.FirstOrDefault(e => e.Dna == predictedDna);
        return match.Id > 0
            ? (match.Id, MaterialLabelFormatter.Format(match.Id, predictedDna))
            : traced;
    }

    private static string? ResolvePredictedDepositPhase(
        string? linkedMachineId,
        string? linkedPort,
        IReadOnlyDictionary<string, MachineInfo> machineById,
        IReadOnlyList<ConnectionInfo> connections,
        BoardLineState? runtime)
    {
        if (linkedMachineId == null || linkedPort == null)
            return null;
        if (!machineById.TryGetValue(linkedMachineId, out var machine))
            return null;

        var traced = MaterialFlowTrace.TraceUpstream(
            linkedMachineId, linkedPort, machineById, connections, runtime);
        if (traced.ElementId == null)
            return null;

        long? inputDna = null;
        var inPort = MaterialFlowTrace.ResolveUpstreamInputPort(machine.Type, linkedPort);
        if (inPort != null)
        {
            var inPkt = MaterialFlowTrace.TryReadPacket(runtime, linkedMachineId, inPort, isOutput: false);
            inputDna = inPkt?.Dna;
        }

        var predicted = MaterialFlowTrace.PredictOutput(
            machine, linkedPort, traced.ElementId, traced.ElementSymbol, inputDna);
        return predicted.OutputPhase == null
            ? null
            : MaterialPhaseLabels.PhaseLabel(predicted.OutputPhase);
    }

    private static string? ResolveUpstreamInputPort(string machineType, string outPort) =>
        machineType switch
        {
            _ when machineType.Equals("Boiler", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Heater", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Cooler", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Condenser", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Melter", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Mixer", StringComparison.OrdinalIgnoreCase) => "in1",
            _ when machineType.Equals("Sorter", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("Destilator", StringComparison.OrdinalIgnoreCase) => "in",
            _ when machineType.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase) => "in",
            _ => null
        };

    private static List<int> ParseSorterOutElements(string? settingsJson, string outPort)
    {
        var portKey = outPort.ToLowerInvariant() switch
        {
            "out1" => "port1",
            "out2" => "port2",
            "out3" => "port3",
            _ => null
        };
        if (portKey == null || string.IsNullOrEmpty(settingsJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (!doc.RootElement.TryGetProperty(portKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return [];
            var ids = new List<int>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                    ids.Add(id);
            }
            return ids;
        }
        catch
        {
            return [];
        }
    }

    private static string FormatPath(string? fromId, string? fromPort, string? toId, string? toPort) =>
        $"{fromId}.{fromPort} → {toId}.{toPort}";

}

public sealed record SeaportPortFlowDetail(
    string MachineId,
    string MachineType,
    string Port,
    string Direction,
    bool IsConnected,
    string? LinkedMachineId,
    string? LinkedPort,
    int? ElementId,
    string? ElementSymbol,
    string Summary,
    bool IsEstimate);
