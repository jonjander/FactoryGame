using System.Text.Json;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation.Processors;

namespace FactoryGame.Domain.Simulation;

internal static class MaterialFlowTrace
{
    internal static MaterialPacket? TryReadPacket(BoardLineState? runtime, string machineId, string port, bool isOutput)
    {
        if (runtime == null || !runtime.Machines.TryGetValue(machineId, out var machine))
            return null;

        var buffers = isOutput ? machine.OutputPorts : machine.InputPorts;
        if (!buffers.TryGetValue(port, out var buffer))
            return null;

        return buffer.Snapshot().FirstOrDefault();
    }

    internal static (int? ElementId, string? ElementSymbol) TraceUpstream(
        string machineId,
        string portName,
        IReadOnlyDictionary<string, MachineInfo> machineById,
        IReadOnlyList<ConnectionInfo> connections,
        BoardLineState? runtime,
        HashSet<string>? visited = null)
    {
        visited ??= new HashSet<string>(StringComparer.Ordinal);
        var key = $"{machineId}\0{portName}";
        if (!visited.Add(key))
            return (null, null);

        var pkt = TryReadPacket(runtime, machineId, portName, isOutput: true)
                  ?? TryReadPacket(runtime, machineId, portName, isOutput: false);
        if (pkt != null)
            return (pkt.ElementId, SymbolFor(pkt.ElementId));

        if (!machineById.TryGetValue(machineId, out var machine))
            return (null, null);

        var settings = machine.Settings?.GetRawText();

        if ((machine.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
             || machine.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase))
            && portName.Equals("out", StringComparison.OrdinalIgnoreCase))
        {
            var id = SeaportConnectorProcessor.ParseOutElementId(settings);
            return id > 0 ? (id, SymbolFor(id)) : (null, null);
        }

        if (machine.Type.Equals("Sorter", StringComparison.OrdinalIgnoreCase)
            && portName.StartsWith("out", StringComparison.OrdinalIgnoreCase))
        {
            var ids = ParseSorterOutElements(settings, portName);
            if (ids.Count == 1)
                return (ids[0], SymbolFor(ids[0]));
            return (null, null);
        }

        var inPort = ResolveUpstreamInputPort(machine.Type, portName);
        if (inPort == null)
            return (null, null);

        var connTo = connections.FirstOrDefault(c => c.ToId == machineId && c.ToPort == inPort);
        if (connTo == null)
            return (null, null);

        return TraceUpstream(connTo.FromId, connTo.FromPort, machineById, connections, runtime, visited);
    }

    internal static PredictedOutput PredictOutput(
        MachineInfo machine,
        string outPort,
        int? inputElementId,
        string? inputElementSymbol,
        long? inputDna = null)
    {
        if (inputElementId == null)
            return new PredictedOutput(null, null, null, null, null, null, false);

        var sourceDna = inputDna ?? ElementCatalogLookup.CatalogDnaFor(inputElementId.Value);
        var inputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(sourceDna));

        var inputEl = ElementCatalog.All.FirstOrDefault(e => e.Id == inputElementId.Value);
        if (inputEl.Id != inputElementId.Value)
            return new PredictedOutput(inputElementId, inputElementSymbol, sourceDna, inputPhase, inputPhase, null, false);

        long outputDna;
        string? note;

        if (machine.Type.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
        {
            outputDna = DnaTransforms.Heat(sourceDna);
            note = "värms i Boiler";
        }
        else if (machine.Type.Equals("Heater", StringComparison.OrdinalIgnoreCase))
        {
            outputDna = DnaTransforms.Heat(sourceDna, 4);
            note = "värms i Heater";
        }
        else if (machine.Type.Equals("Cooler", StringComparison.OrdinalIgnoreCase))
        {
            outputDna = DnaTransforms.Cool(sourceDna);
            note = "kyls i Cooler";
        }
        else if (machine.Type.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
        {
            outputDna = DnaTransforms.Condense(sourceDna);
            note = "kondenseras till vätska";
        }
        else if (machine.Type.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase))
        {
            var cut = 2048;
            var (dna, solid) = DnaTransforms.Crystallize(sourceDna, cut);
            outputDna = dna;
            note = solid ? "kristalliseras till fast form" : "kyls (fryspunkt över cut, fortfarande vätska)";
        }
        else if (machine.Type.Equals("Melter", StringComparison.OrdinalIgnoreCase))
        {
            var (dna, melted) = DnaTransforms.Melt(sourceDna, 1800);
            outputDna = dna;
            note = melted ? "smälts till vätska" : "värms (kokpunkt under cut, fortfarande fast)";
        }
        else if (machine.Type.Equals("Mixer", StringComparison.OrdinalIgnoreCase))
        {
            return new PredictedOutput(
                inputElementId, inputElementSymbol, sourceDna, inputPhase, inputPhase,
                "blandas i Mixer (kräver två ingångar; intensitet styr kvalitet)", false);
        }
        else if (machine.Type.Equals("Sorter", StringComparison.OrdinalIgnoreCase))
        {
            var ids = ParseSorterOutElements(machine.Settings?.GetRawText(), outPort);
            if (ids.Count == 1)
                return new PredictedOutput(ids[0], SymbolFor(ids[0]), ElementCatalogLookup.CatalogDnaFor(ids[0]), inputPhase, inputPhase, "sorteras ut", false);
            if (ids.Count > 1)
                return new PredictedOutput(null, null, null, inputPhase, null, $"sorter: {string.Join(", ", ids.Select(SymbolFor))}", false);
            return new PredictedOutput(inputElementId, inputElementSymbol, sourceDna, inputPhase, inputPhase, null, false);
        }
        else
        {
            return new PredictedOutput(inputElementId, inputElementSymbol, sourceDna, inputPhase, inputPhase, null, false);
        }

        var resolved = ResolveOutput(outputDna, inputElementId, inputElementSymbol, note);
        var outputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(outputDna));
        var dnaChanged = sourceDna != outputDna;
        if (inputPhase != outputPhase && note != null)
            note = $"{MaterialPhaseLabels.PhaseLabelSv(inputPhase)} → {MaterialPhaseLabels.PhaseLabelSv(outputPhase)}";

        return new PredictedOutput(
            resolved.OutputId ?? inputElementId,
            resolved.OutputSymbol ?? inputElementSymbol,
            outputDna,
            inputPhase,
            outputPhase,
            resolved.TransformNote ?? note,
            dnaChanged);
    }

    private static (int? OutputId, string? OutputSymbol, string? TransformNote) ResolveOutput(
        long dna,
        int? inputElementId,
        string? inputSymbol,
        string transformNote)
    {
        var match = ElementCatalog.All.FirstOrDefault(e => e.Dna == dna);
        if (match.Id > 0)
            return (match.Id, match.Symbol, transformNote);

        if (inputElementId is > 0 && !string.IsNullOrEmpty(inputSymbol))
            return (inputElementId, inputSymbol, transformNote);

        return (null, inputSymbol != null ? $"{inputSymbol}*" : null, $"{transformNote} (nytt DNA)");
    }

    internal static string? ResolveUpstreamInputPort(string machineType, string outPort) =>
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

    internal static string? SymbolFor(int elementId)
    {
        var el = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId);
        return el.Id == elementId ? el.Symbol : null;
    }
}

internal sealed record PredictedOutput(
    int? OutputId,
    string? OutputSymbol,
    long? OutputDna,
    string? InputPhase,
    string? OutputPhase,
    string? TransformNote,
    bool DnaChanged);
