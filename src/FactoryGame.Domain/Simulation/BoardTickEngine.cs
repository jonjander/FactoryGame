using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Simulation;

/// <summary>Advances one deterministic simulation tick for a running board plan.</summary>
public static class BoardTickEngine
{
    public static BoardTickResult Advance(
        SimulationPlan plan,
        BoardLineState? previousState,
        long tick,
        decimal unitsPerTick,
        ISeaportPoolSink? pool)
    {
        var state = previousState?.CloneShallow() ?? new BoardLineState();
        var ctx = new TickContext
        {
            Tick = tick,
            UnitsPerTick = unitsPerTick,
            Plan = plan,
            Pool = pool
        };

        foreach (var m in plan.Machines)
        {
            var runtime = state.GetOrCreate(m.Id, m.Type);
            FlowHelper.InitPortsForMachine(runtime, m.Type);
        }

        var order = PlanGraph.MachineProcessingOrder(plan, out _);

        var machineMeta = plan.Machines.ToDictionary(m => m.Id, m => m, StringComparer.Ordinal);
        var transferPasses = Math.Max(2, plan.Connections.Count);

        for (var pass = 0; pass < transferPasses; pass++)
        {
            foreach (var machineId in order)
            {
                if (!machineMeta.TryGetValue(machineId, out _))
                    continue;
                TransferInputs(plan, state, machineMeta, state.Machines[machineId], ctx);
            }
        }

        foreach (var machineId in order)
        {
            if (!machineMeta.TryGetValue(machineId, out var meta))
                continue;
            var runtime = state.Machines[machineId];

            if (!runtime.IsBlocked)
            {
                var processor = MachineProcessorRegistry.Resolve(meta.Type);
                processor.Process(runtime, ctx, meta.SettingsJson);
            }
        }

        var active = state.Machines.Values.Count(m => !m.IsBlocked);
        var note = $"tick={tick};active={active};withdrawn={ctx.SeaportDelta.WithdrawnFromPool.Values.Sum()};deposited={ctx.SeaportDelta.DepositedToPool.Values.Sum()}";

        return new BoardTickResult
        {
            State = state,
            SeaportDelta = ctx.SeaportDelta,
            Tick = tick,
            SummaryNote = note
        };
    }

    private static void TransferInputs(
        SimulationPlan plan,
        BoardLineState state,
        IReadOnlyDictionary<string, SimulationMachine> machineMeta,
        MachineRuntimeState target,
        TickContext ctx)
    {
        foreach (var c in plan.Connections.Where(x => x.ToId == target.MachineId))
        {
            if (!state.Machines.TryGetValue(c.FromId, out var source))
                continue;
            if (!machineMeta.TryGetValue(c.FromId, out var sourceMeta))
                continue;
            if (!machineMeta.TryGetValue(c.ToId, out var targetMeta))
                continue;
            if (!source.OutputPorts.TryGetValue(c.FromPort, out var fromBuf))
                continue;
            if (!target.InputPorts.TryGetValue(c.ToPort, out var toBuf))
                continue;

            var maxQty = MachineRateCatalog.GetConnectionTransferBudget(
                sourceMeta.Type,
                c.FromPort,
                sourceMeta.SettingsJson,
                targetMeta.Type,
                c.ToPort,
                targetMeta.SettingsJson,
                ctx.UnitsPerTick);

            FlowHelper.TryMovePortToPort(fromBuf, toBuf, maxQty);
        }
    }

    public static BoardLineState CreateInitialState(SimulationPlan plan)
    {
        var state = new BoardLineState();
        foreach (var m in plan.Machines)
        {
            var runtime = state.GetOrCreate(m.Id, m.Type);
            FlowHelper.InitPortsForMachine(runtime, m.Type);
        }
        return state;
    }
}
