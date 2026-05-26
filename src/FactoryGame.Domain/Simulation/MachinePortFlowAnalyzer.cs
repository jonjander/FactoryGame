using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
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
                string? inputPhase = null;
                string? outputPhase = null;
                long? inputDna = null;
                long? outputDna = outPkt?.Dna;
                var processStatus = MaterialProcessStatus.Idle;
                var dnaChanged = false;

                if (isPoolSource)
                {
                    var poolId = SeaportConnectorProcessor.ParseOutElementId(machine.Settings?.GetRawText());
                    var poolDna = SeaportConnectorProcessor.ResolveOutMaterialDna(machine.Settings?.GetRawText(), poolId);
                    outputId = poolId > 0 ? poolId : null;
                    outputDna = poolId > 0 ? poolDna : null;
                    outputSymbol = outputId != null ? MaterialFlowTrace.SymbolFor(outputId.Value) : null;
                    if (outputDna is { } pd)
                    {
                        var phase = MaterialPhaseLabels.DecodePhase(pd);
                        outputPhase = MaterialPhaseLabels.PhaseKey(phase);
                    }
                    transformNote = "från pool";
                    processStatus = outputId > 0 ? MaterialProcessStatus.WaitingMaterial : MaterialProcessStatus.Idle;
                }
                else if (link != null)
                {
                    var inPort = MaterialFlowTrace.ResolveUpstreamInputPort(machine.Type, port.Name);
                    var inPkt = inPort != null
                        ? MaterialFlowTrace.TryReadPacket(runtime, machine.Id, inPort, isOutput: false)
                        : null;

                    if (inPort != null)
                    {
                        var connIn = connections.FirstOrDefault(c =>
                            c.ToId == machine.Id && c.ToPort.Equals(inPort, StringComparison.OrdinalIgnoreCase));
                        if (connIn != null)
                        {
                            var source = MaterialFlowTrace.TraceUpstream(
                                connIn.FromId, connIn.FromPort, machineById, connections, runtime,
                                new HashSet<string>(StringComparer.Ordinal));
                            inputId = source.ElementId;
                            inputSymbol = source.ElementSymbol;
                        }
                    }

                    if (inPkt != null)
                    {
                        inputDna = inPkt.Dna;
                        inputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(inPkt.Dna));
                        inputId ??= inPkt.ElementId;
                        inputSymbol ??= MaterialFlowTrace.SymbolFor(inPkt.ElementId);
                    }
                    else if (inputId != null && inputDna == null)
                    {
                        inputDna = ResolveUpstreamSourceDna(machine, inPort, connections, machineById);
                        if (inputDna is { } dna)
                            inputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(dna));
                    }

                    if (outPkt != null)
                    {
                        outputId = outPkt.ElementId;
                        outputSymbol = MaterialFlowTrace.SymbolFor(outPkt.ElementId);
                        outputDna = outPkt.Dna;
                        outputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(outPkt.Dna));

                        if (inPkt != null)
                        {
                            dnaChanged = inPkt.Dna != outPkt.Dna;
                            transformNote = BuildRuntimeTransformNote(machine.Type, inPkt, outPkt);
                            processStatus = dnaChanged || inputPhase != outputPhase
                                ? MaterialProcessStatus.Transformed
                                : MaterialProcessStatus.Processing;
                        }
                        else if (inputId != null)
                        {
                            var sourceDna = inputDna ?? ElementCatalogLookup.CatalogDnaFor(inputId.Value);
                            inputDna ??= sourceDna;
                            inputPhase ??= MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(sourceDna));
                            dnaChanged = sourceDna != outPkt.Dna || inputPhase != outputPhase;
                            transformNote = inputPhase != outputPhase
                                ? $"{MaterialPhaseLabels.PhaseLabelSv(inputPhase)} → {MaterialPhaseLabels.PhaseLabelSv(outputPhase)}"
                                : MaterialFlowTrace.PredictOutput(
                                    machine, port.Name, inputId, inputSymbol, sourceDna).TransformNote;
                            processStatus = dnaChanged || inputPhase != outputPhase
                                ? MaterialProcessStatus.Transformed
                                : MaterialProcessStatus.Processing;
                        }
                    }
                    else if (inputId != null)
                    {
                        var predicted = MaterialFlowTrace.PredictOutput(
                            machine, port.Name, inputId, inputSymbol, inputDna);
                        outputId = predicted.OutputId ?? inputId;
                        outputSymbol = predicted.OutputSymbol ?? inputSymbol;
                        outputDna = predicted.OutputDna ?? inputDna;
                        transformNote = predicted.TransformNote;
                        inputPhase = predicted.InputPhase ?? inputPhase;
                        outputPhase = predicted.OutputPhase;
                        dnaChanged = predicted.DnaChanged;
                        processStatus = isRunning
                            ? MaterialProcessStatus.WaitingMaterial
                            : outputPhase != inputPhase || dnaChanged
                                ? MaterialProcessStatus.Transformed
                                : MaterialProcessStatus.Idle;
                    }
                }

                if (isRunning && runtime != null
                    && runtime.Machines.TryGetValue(machine.Id, out var runtimeMachine)
                    && !string.IsNullOrEmpty(runtimeMachine.BlockedReason))
                    processStatus = MaterialProcessStatus.Blocked;

                var summary = BuildSummary(
                    inputSymbol, outputSymbol, inputPhase, outputPhase, transformNote, link, isPoolSource);

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
                    inputPhase,
                    outputPhase,
                    inputDna,
                    outputDna,
                    transformNote,
                    summary,
                    processStatus,
                    dnaChanged,
                    isEstimate,
                    isPoolSource));
            }
        }

        return results;
    }

    private static long? ResolveUpstreamSourceDna(
        MachineInfo machine,
        string? inPort,
        IReadOnlyList<ConnectionInfo> connections,
        IReadOnlyDictionary<string, MachineInfo> machineById)
    {
        if (inPort == null)
            return null;

        var connIn = connections.FirstOrDefault(c =>
            c.ToId == machine.Id && c.ToPort.Equals(inPort, StringComparison.OrdinalIgnoreCase));
        if (connIn == null || !machineById.TryGetValue(connIn.FromId, out var sourceMachine))
            return null;

        var settings = sourceMachine.Settings?.GetRawText();
        if (sourceMachine.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
            || sourceMachine.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase))
        {
            var elementId = SeaportConnectorProcessor.ParseOutElementId(settings);
            if (elementId <= 0)
                return null;
            return SeaportConnectorProcessor.ResolveOutMaterialDna(settings, elementId);
        }

        return null;
    }

    private static string? BuildRuntimeTransformNote(string machineType, MaterialPacket inPkt, MaterialPacket outPkt)
    {
        var inPhase = MaterialPhaseLabels.DecodePhase(inPkt.Dna);
        var outPhase = MaterialPhaseLabels.DecodePhase(outPkt.Dna);
        if (inPhase != outPhase)
        {
            return $"{MaterialPhaseLabels.PhaseLabelSv(inPhase)} → {MaterialPhaseLabels.PhaseLabelSv(outPhase)}";
        }

        if (inPkt.Dna != outPkt.Dna)
            return MaterialFlowTrace.PredictOutput(
                new MachineInfo("", machineType, null), "out", inPkt.ElementId,
                MaterialFlowTrace.SymbolFor(inPkt.ElementId), inPkt.Dna).TransformNote;

        return null;
    }

    private static bool IsPoolOutMachine(string type, string portName) =>
        portName.Equals("out", StringComparison.OrdinalIgnoreCase)
        && (type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
            || type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase));

    private static string BuildSummary(
        string? inputSymbol,
        string? outputSymbol,
        string? inputPhase,
        string? outputPhase,
        string? transformNote,
        ConnectionInfo? link,
        bool isPoolSource)
    {
        if (isPoolSource)
        {
            var dest = link != null ? $" → {link.ToId}.{link.ToPort}" : " (ej kopplad)";
            if (outputSymbol != null && outputPhase != null)
                return $"Pool → fabrik: {outputSymbol} ({MaterialPhaseLabels.PhaseLabelSv(outputPhase)}){dest}";
            return outputSymbol != null
                ? $"Pool → fabrik: {outputSymbol}{dest}"
                : $"Pool → fabrik{dest} — välj variant";
        }

        if (link == null)
            return "Utgång ej kopplad.";

        var phaseArrow = inputPhase != null && outputPhase != null && inputPhase != outputPhase
            ? $" ({MaterialPhaseLabels.PhaseLabelSv(inputPhase)} → {MaterialPhaseLabels.PhaseLabelSv(outputPhase)})"
            : "";

        if (inputSymbol != null && outputSymbol != null)
        {
            if (inputSymbol != outputSymbol || !string.IsNullOrEmpty(phaseArrow))
                return $"{inputSymbol} → {outputSymbol}{phaseArrow} → {link.ToId}.{link.ToPort}";
            return $"{outputSymbol} till {link.ToId}.{link.ToPort}";
        }

        if (!string.IsNullOrEmpty(transformNote))
            return $"{transformNote} → {link.ToId}.{link.ToPort}";

        return $"Material → {link.ToId}.{link.ToPort}";
    }
}

public static class MaterialProcessStatus
{
    public const string Idle = "Idle";
    public const string WaitingMaterial = "WaitingMaterial";
    public const string Processing = "Processing";
    public const string Transformed = "Transformed";
    public const string Blocked = "Blocked";
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
    string? InputPhase,
    string? OutputPhase,
    long? InputDna,
    long? OutputDna,
    string? TransformNote,
    string Summary,
    string ProcessStatus,
    bool DnaChanged,
    bool IsEstimate,
    bool IsPoolSource);
