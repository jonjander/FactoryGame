using FactoryGame.Domain.Content;
using FactoryGame.Domain.Simulation.Processors;

namespace FactoryGame.Domain.Simulation;

/// <summary>Per output-port material hints (input element → output element) for factory UI.</summary>
public static class MachinePortFlowAnalyzer
{
    public static IReadOnlyList<MachinePortFlowDetail> Analyze(
        IReadOnlyList<MachineInfo> machines,
        IReadOnlyList<ConnectionInfo> connections,
        bool isRunning,
        BoardLineState? runtime)
    {
        var connFrom = connections.GroupBy(c => (c.FromId, c.FromPort))
            .ToDictionary(g => g.Key, g => g.First());
        var machineById = machines.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var isEstimate = !isRunning || runtime == null;
        var results = new List<MachinePortFlowDetail>();

        foreach (var machine in machines)
        {
            if (!MachinePortCatalog.IsKnownMachineType(machine.Type))
                continue;

            foreach (var port in MachinePortCatalog.GetPorts(machine.Type))
            {
                if (port.Direction != PortDirection.Out)
                    continue;

                connFrom.TryGetValue((machine.Id, port.Name), out var link);
                var isPoolSource = IsPoolOutMachine(machine.Type, port.Name);

                var outPkt = MaterialFlowTrace.TryReadPacket(runtime, machine.Id, port.Name, isOutput: true);
                int? inputId = null;
                string? inputSymbol = null;
                int? outputId = outPkt?.ElementId;
                string? outputSymbol = outputId != null ? MaterialFlowTrace.SymbolFor(outputId.Value) : null;
                string? transformNote = null;

                if (isPoolSource)
                {
                    var poolId = SeaportConnectorProcessor.ParseOutElementId(machine.Settings?.GetRawText());
                    outputId = poolId > 0 ? poolId : 1;
                    outputSymbol = MaterialFlowTrace.SymbolFor(outputId.Value);
                    inputId = null;
                    inputSymbol = null;
                    transformNote = "från pool";
                }
                else if (link != null)
                {
                    var upstream = MaterialFlowTrace.TraceUpstream(
                        machine.Id, port.Name, machineById, connections, runtime,
                        new HashSet<string>(StringComparer.Ordinal));
                    inputId = upstream.ElementId;
                    inputSymbol = upstream.ElementSymbol;

                    if (outputId == null && inputId != null)
                    {
                        var predicted = MaterialFlowTrace.PredictOutput(
                            machine, port.Name, inputId, inputSymbol);
                        outputId = predicted.OutputId ?? inputId;
                        outputSymbol = predicted.OutputSymbol ?? inputSymbol;
                        transformNote = predicted.TransformNote;
                    }
                }

                var summary = BuildSummary(
                    machine, port.Name, isPoolSource, inputSymbol, outputSymbol, transformNote, link);

                results.Add(new MachinePortFlowDetail(
                    machine.Id,
                    machine.Type,
                    port.Name,
                    link?.ToId,
                    link?.ToPort,
                    inputId,
                    inputSymbol,
                    outputId,
                    outputSymbol,
                    transformNote,
                    summary,
                    isEstimate,
                    isPoolSource));
            }
        }

        return results;
    }

    private static bool IsPoolOutMachine(string type, string portName) =>
        portName.Equals("out", StringComparison.OrdinalIgnoreCase)
        && (type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
            || type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase));

    private static string BuildSummary(
        MachineInfo machine,
        string portName,
        bool isPoolSource,
        string? inputSymbol,
        string? outputSymbol,
        string? transformNote,
        ConnectionInfo? link)
    {
        if (isPoolSource)
        {
            var dest = link != null ? $" → {link.ToId}.{link.ToPort}" : " (ej kopplad)";
            return outputSymbol != null
                ? $"Pool → fabrik: {outputSymbol}{dest}"
                : $"Pool → fabrik{dest} — välj grundämne";
        }

        if (link == null)
            return $"Utgång «{portName}» ej kopplad.";

        if (inputSymbol != null && outputSymbol != null && inputSymbol != outputSymbol)
            return $"{inputSymbol} → {outputSymbol} via {machine.Id}.{portName} → {link.ToId}.{link.ToPort}";

        if (outputSymbol != null)
            return $"{outputSymbol} till {link.ToId}.{link.ToPort}";

        if (transformNote != null)
            return $"{transformNote} → {link.ToId}.{link.ToPort}";

        return $"Material via {machine.Id}.{portName} → {link.ToId}.{link.ToPort}";
    }
}

public sealed record MachinePortFlowDetail(
    string MachineId,
    string MachineType,
    string Port,
    string? LinkedMachineId,
    string? LinkedPort,
    int? InputElementId,
    string? InputElementSymbol,
    int? OutputElementId,
    string? OutputElementSymbol,
    string? TransformNote,
    string Summary,
    bool IsEstimate,
    bool IsPoolSource);
