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
                ProcessingSlotState? processingSlot = null;
                if (isRunning && runtime?.Machines.TryGetValue(machine.Id, out var runtimeMachine) == true)
                    processingSlot = runtimeMachine.ProcessingSlot;
                var slotPkt = processingSlot?.Packet;
                int? inputId = null;
                string? inputSymbol = null;
                int? outputId = outPkt?.ElementId;
                string? outputSymbol = outPkt != null
                    ? MaterialFlowTrace.SymbolForPacket(outPkt.ElementId, outPkt.Dna)
                    : outputId != null ? MaterialFlowTrace.SymbolFor(outputId.Value) : null;
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
                    outputSymbol = outputId != null && outputDna is { } poolOutDna
                        ? MaterialFlowTrace.SymbolForPacket(outputId.Value, poolOutDna)
                        : outputId != null ? MaterialFlowTrace.SymbolFor(outputId.Value) : null;
                    if (outputDna is { } pd)
                    {
                        var phase = MaterialPhaseLabels.DecodePhase(pd);
                        outputPhase = MaterialPhaseLabels.PhaseKey(phase);
                    }
                    transformNote = "from pool";
                    processStatus = outPkt != null
                        ? MaterialProcessStatus.Processing
                        : outputId > 0
                            ? MaterialProcessStatus.WaitingMaterial
                            : MaterialProcessStatus.Idle;
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

                    if (inPkt == null && slotPkt != null)
                    {
                        inPkt = slotPkt;
                        inputDna = slotPkt.Dna;
                        inputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(slotPkt.Dna));
                        inputId = slotPkt.ElementId;
                        inputSymbol = MaterialFlowTrace.SymbolForPacket(slotPkt.ElementId, slotPkt.Dna);
                    }

                    if (inPkt != null)
                    {
                        inputDna = inPkt.Dna;
                        inputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(inPkt.Dna));
                        inputId ??= inPkt.ElementId;
                        inputSymbol ??= MaterialFlowTrace.SymbolForPacket(inPkt.ElementId, inPkt.Dna);
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
                        outputSymbol = MaterialFlowTrace.SymbolForPacket(outPkt.ElementId, outPkt.Dna);
                        outputDna = outPkt.Dna;
                        outputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(outPkt.Dna));

                        if (inPkt != null)
                        {
                            dnaChanged = inPkt.Dna != outPkt.Dna;
                            transformNote = BuildRuntimeTransformNote(machine, inPkt, outPkt);
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
                                ? $"{MaterialPhaseLabels.PhaseLabel(inputPhase)} → {MaterialPhaseLabels.PhaseLabel(outputPhase)}"
                                : MaterialFlowTrace.PredictOutput(
                                    machine, port.Name, inputId, inputSymbol, sourceDna).TransformNote;
                            processStatus = dnaChanged || inputPhase != outputPhase
                                ? MaterialProcessStatus.Transformed
                                : MaterialProcessStatus.Processing;
                        }
                    }
                    else if (inputId != null || inPkt != null)
                    {
                        var activeInputDna = inPkt?.Dna ?? inputDna;
                        var activeInputId = inPkt?.ElementId ?? inputId;
                        var predicted = machine.Type.Equals("Mixer", StringComparison.OrdinalIgnoreCase)
                            ? TryPredictMixerOutput(machine, connections, machineById)
                            ?? MaterialFlowTrace.PredictOutput(
                                machine, port.Name, activeInputId, inputSymbol, activeInputDna)
                            : MaterialFlowTrace.PredictOutput(
                                machine, port.Name, activeInputId, inputSymbol, activeInputDna);
                        outputId = predicted.OutputId ?? activeInputId;
                        outputSymbol = predicted.OutputSymbol ?? inputSymbol;
                        outputDna = predicted.OutputDna ?? activeInputDna;
                        transformNote = predicted.TransformNote;
                        inputPhase = predicted.InputPhase ?? inputPhase;
                        outputPhase = predicted.OutputPhase;
                        dnaChanged = predicted.DnaChanged;

                        if (isRunning && slotPkt != null && outPkt == null)
                        {
                            processStatus = MaterialProcessStatus.Processing;
                            transformNote = BuildProcessingSlotNote(processingSlot, predicted.TransformNote);
                        }
                        else
                        {
                            processStatus = isRunning
                                ? MaterialProcessStatus.WaitingMaterial
                                : outputPhase != inputPhase || dnaChanged
                                    ? MaterialProcessStatus.Transformed
                                    : MaterialProcessStatus.Idle;
                        }
                    }
                }
                else if (machine.Type.Equals("Mixer", StringComparison.OrdinalIgnoreCase))
                {
                    var predicted = TryPredictMixerOutput(machine, connections, machineById);
                    if (predicted != null)
                    {
                        outputId = predicted.OutputId;
                        outputSymbol = predicted.OutputSymbol;
                        outputDna = predicted.OutputDna;
                        transformNote = predicted.TransformNote;
                        inputPhase = predicted.InputPhase;
                        outputPhase = predicted.OutputPhase;
                        dnaChanged = predicted.DnaChanged;
                        processStatus = dnaChanged
                            ? MaterialProcessStatus.Transformed
                            : MaterialProcessStatus.Idle;
                    }
                }

                if (isRunning && runtime != null
                    && runtime.Machines.TryGetValue(machine.Id, out var blockedMachine)
                    && !string.IsNullOrEmpty(blockedMachine.BlockedReason))
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
        if (sourceMachine.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
        {
            var elementId = SeaportConnectorProcessor.ParseOutElementId(settings);
            if (elementId <= 0)
                return null;
            return SeaportConnectorProcessor.ResolveOutMaterialDna(settings, elementId);
        }

        return null;
    }

    private static PredictedOutput? TryPredictMixerOutput(
        MachineInfo machine,
        IReadOnlyList<ConnectionInfo> connections,
        IReadOnlyDictionary<string, MachineInfo> machineById)
    {
        var dnaA = ResolveUpstreamSourceDna(machine, "in1", connections, machineById);
        var dnaB = ResolveUpstreamSourceDna(machine, "in2", connections, machineById);
        var idA = ResolveUpstreamElementId(machine, "in1", connections, machineById);
        var idB = ResolveUpstreamElementId(machine, "in2", connections, machineById);
        if (dnaA == null || dnaB == null || idA == null || idB == null)
            return null;

        var settings = machine.Settings?.GetRawText();
        var ratio = MachineSettingsJson.ReadInt(settings, 500, 100, 900, "ratioPermille", "ratio");
        var intensity = MachineSettingsJson.ReadInt(settings, 350, 100, 1000,
            "mixIntensityPermille", "mixIntensity", "intensity");
        var (outDna, tier) = DnaTransforms.MixCombined(dnaA.Value, dnaB.Value, ratio, intensity);
        var dominantId = ratio >= 500 ? idA.Value : idB.Value;
        var inputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(dnaA.Value));
        var outputPhase = MaterialPhaseLabels.PhaseKey(MaterialPhaseLabels.DecodePhase(outDna));
        var inSym = MaterialFlowTrace.SymbolForPacket(idA.Value, dnaA.Value);
        var in2Sym = MaterialFlowTrace.SymbolForPacket(idB.Value, dnaB.Value);
        var outSym = MaterialFlowTrace.SymbolForPacket(dominantId, outDna);
        var note = tier switch
        {
            MixTier.Volatile => $"mixed {inSym}+{in2Sym} (volatile)",
            MixTier.Refined => $"mixed {inSym}+{in2Sym} (refined)",
            _ => $"mixed {inSym}+{in2Sym} (poor)"
        };
        return new PredictedOutput(
            dominantId,
            outSym,
            outDna,
            inputPhase,
            outputPhase,
            note,
            dnaA != dnaB || outDna != dnaA || outDna != dnaB);
    }

    private static int? ResolveUpstreamElementId(
        MachineInfo machine,
        string inPort,
        IReadOnlyList<ConnectionInfo> connections,
        IReadOnlyDictionary<string, MachineInfo> machineById)
    {
        var connIn = connections.FirstOrDefault(c =>
            c.ToId == machine.Id && c.ToPort.Equals(inPort, StringComparison.OrdinalIgnoreCase));
        if (connIn == null || !machineById.TryGetValue(connIn.FromId, out var sourceMachine))
            return null;

        var settings = sourceMachine.Settings?.GetRawText();
        if (sourceMachine.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
        {
            var elementId = SeaportConnectorProcessor.ParseOutElementId(settings);
            return elementId > 0 ? elementId : null;
        }

        return null;
    }

    private static string? BuildProcessingSlotNote(ProcessingSlotState? slot, string? predictedNote)
    {
        if (slot?.Packet == null)
            return predictedNote;

        var progress = slot.TotalTicks > 0
            ? $"step {Math.Min(slot.ElapsedTicks, slot.TotalTicks)}/{slot.TotalTicks}"
            : null;
        var kind = string.IsNullOrEmpty(slot.ProcessKind) ? "processing" : slot.ProcessKind;

        if (!string.IsNullOrEmpty(predictedNote) && progress != null)
            return $"{kind} ({progress}) — {predictedNote}";
        if (progress != null)
            return $"{kind} ({progress})";
        return predictedNote;
    }

    private static string? BuildRuntimeTransformNote(MachineInfo machine, MaterialPacket inPkt, MaterialPacket outPkt)
    {
        var inPhase = MaterialPhaseLabels.DecodePhase(inPkt.Dna);
        var outPhase = MaterialPhaseLabels.DecodePhase(outPkt.Dna);
        if (inPhase != outPhase)
        {
            return $"{MaterialPhaseLabels.PhaseLabel(inPhase)} → {MaterialPhaseLabels.PhaseLabel(outPhase)}";
        }

        if (inPkt.Dna != outPkt.Dna)
            return MaterialFlowTrace.PredictOutput(
                machine, "out", inPkt.ElementId,
                MaterialFlowTrace.SymbolFor(inPkt.ElementId), inPkt.Dna).TransformNote;

        return null;
    }

    private static bool IsPoolOutMachine(string type, string portName) =>
        portName.Equals("out", StringComparison.OrdinalIgnoreCase)
        && type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase);

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
            var dest = link != null ? $" → {link.ToId}.{link.ToPort}" : " (not connected)";
            if (outputSymbol != null && outputPhase != null)
                return $"Pool → factory: {outputSymbol} ({MaterialPhaseLabels.PhaseLabel(outputPhase)}){dest}";
            return outputSymbol != null
                ? $"Pool → factory: {outputSymbol}{dest}"
                : $"Pool → factory{dest} — pick variant";
        }

        if (link == null)
            return "Output not connected.";

        var phaseArrow = inputPhase != null && outputPhase != null && inputPhase != outputPhase
            ? $" ({MaterialPhaseLabels.PhaseLabel(inputPhase)} → {MaterialPhaseLabels.PhaseLabel(outputPhase)})"
            : "";

        if (inputSymbol != null && outputSymbol != null)
        {
            if (inputSymbol != outputSymbol || !string.IsNullOrEmpty(phaseArrow))
                return $"{inputSymbol} → {outputSymbol}{phaseArrow} → {link.ToId}.{link.ToPort}";
            return $"{outputSymbol} to {link.ToId}.{link.ToPort}";
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
