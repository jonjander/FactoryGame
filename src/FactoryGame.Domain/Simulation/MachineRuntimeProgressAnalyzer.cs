using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

/// <summary>Runtime progress for factory canvas (step + overall process, input readiness).</summary>
public static class MachineRuntimeProgressAnalyzer
{
    public static IReadOnlyList<MachineRuntimeProgressDetail> Analyze(
        IReadOnlyList<MachineInfo> machines,
        BoardLineState? runtime)
    {
        if (runtime == null || machines.Count == 0)
            return Array.Empty<MachineRuntimeProgressDetail>();

        var results = new List<MachineRuntimeProgressDetail>();
        foreach (var machine in machines)
        {
            if (!MachinePortCatalog.IsKnownMachineType(machine.Type))
                continue;
            if (!runtime.Machines.TryGetValue(machine.Id, out var state))
                continue;

            var detail = AnalyzeMachine(machine, state);
            if (detail != null)
                results.Add(detail);
        }

        return results;
    }

    private static MachineRuntimeProgressDetail? AnalyzeMachine(MachineInfo machine, MachineRuntimeState state)
    {
        var inputNeeds = CollectInputNeeds(machine.Type, state);
        var settings = machine.Settings?.GetRawText();

        if (state.ProcessingSlot is { Packet: not null } slot && slot.TotalTicks > 0)
        {
            var step = Math.Clamp((double)slot.ElapsedTicks / slot.TotalTicks, 0, 1);
            var overall = ResolveMacroProgress(slot, settings);
            var showDual = overall.HasValue && Math.Abs(overall.Value - step) > 0.04;

            return new MachineRuntimeProgressDetail(
                machine.Id,
                machine.Type,
                OverallProgress: showDual ? overall : step,
                StepProgress: showDual ? step : null,
                slot.ProcessKind,
                IsActive: true,
                inputNeeds);
        }

        if (inputNeeds.Count > 1)
        {
            var ready = inputNeeds.Count(n => n.Ready);
            if (ready < inputNeeds.Count)
            {
                return new MachineRuntimeProgressDetail(
                    machine.Id,
                    machine.Type,
                    OverallProgress: (double)ready / inputNeeds.Count,
                    StepProgress: null,
                    "waiting_inputs",
                    IsActive: ready > 0,
                    inputNeeds);
            }
        }

        if (inputNeeds.Count == 1 && !inputNeeds[0].Ready && HasInputPorts(machine.Type))
        {
            return new MachineRuntimeProgressDetail(
                machine.Id,
                machine.Type,
                OverallProgress: 0,
                StepProgress: null,
                "waiting_material",
                IsActive: false,
                inputNeeds);
        }

        if (inputNeeds.Any(n => n.Ready) && state.ProcessingSlot?.Packet == null)
        {
            return new MachineRuntimeProgressDetail(
                machine.Id,
                machine.Type,
                OverallProgress: 0.08,
                StepProgress: null,
                "buffered",
                IsActive: true,
                inputNeeds);
        }

        return null;
    }

    private static bool HasInputPorts(string machineType) =>
        MachinePortCatalog.GetPorts(machineType).Any(p => p.Direction == PortDirection.In);

    private static double? ResolveMacroProgress(ProcessingSlotState slot, string? settingsJson)
    {
        var decoded = DnaDecoder.Decode(slot.Packet!.Dna);
        return slot.ProcessKind switch
        {
            "melt" => EstimateAscending(
                decoded.BoilingPoint,
                MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.BoilingMask,
                    "cutBoiling", "cutPoint", "cut", "targetBoil")),
            "cool" or "crystallize" => EstimateDescending(
                decoded.FreezePoint,
                MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.FreezeMask,
                    "cutFreeze", "cutPoint", "cut", "targetFreeze")),
            _ => null
        };
    }

    private static double? EstimateAscending(int band, int cut) =>
        cut > 0 ? Math.Clamp((double)band / cut, 0, 0.98) : null;

    private static double? EstimateDescending(int band, int cut) =>
        cut > 0 ? Math.Clamp(1.0 - (double)band / cut, 0, 0.98) : null;

    private static IReadOnlyList<MachineInputNeedDetail> CollectInputNeeds(string machineType, MachineRuntimeState state)
    {
        var ports = MachinePortCatalog.GetPorts(machineType)
            .Where(p => p.Direction == PortDirection.In)
            .ToList();
        if (ports.Count == 0)
            return Array.Empty<MachineInputNeedDetail>();

        var needs = new List<MachineInputNeedDetail>(ports.Count);
        foreach (var port in ports)
        {
            var ready = state.InputPorts.TryGetValue(port.Name, out var buf)
                        && buf.Snapshot().Count > 0;
            needs.Add(new MachineInputNeedDetail(port.Name, ready));
        }

        return needs;
    }
}

public sealed record MachineRuntimeProgressDetail(
    string MachineId,
    string MachineType,
    double? OverallProgress,
    double? StepProgress,
    string? ProcessKind,
    bool IsActive,
    IReadOnlyList<MachineInputNeedDetail> InputNeeds);

public sealed record MachineInputNeedDetail(string Port, bool Ready);
