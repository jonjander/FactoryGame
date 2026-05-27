namespace FactoryGame.Domain.Simulation;

public sealed class BoardLineState
{
    public const int RuleVersion = 2;

    public Dictionary<string, MachineRuntimeState> Machines { get; } = new(StringComparer.Ordinal);

    public MachineRuntimeState GetOrCreate(string machineId, string machineType)
    {
        if (Machines.TryGetValue(machineId, out var existing))
            return existing;
        var created = new MachineRuntimeState { MachineId = machineId, MachineType = machineType };
        Machines[machineId] = created;
        return created;
    }

    public BoardLineState CloneShallow()
    {
        var copy = new BoardLineState();
        foreach (var (id, m) in Machines)
        {
            var nm = new MachineRuntimeState
            {
                MachineId = m.MachineId,
                MachineType = m.MachineType,
                BlockedReason = m.BlockedReason,
                Tank = CloneTank(m.Tank),
                Junction = CloneJunction(m.Junction),
                ProcessingSlot = CloneProcessingSlot(m.ProcessingSlot)
            };
            foreach (var (p, buf) in m.InputPorts)
            {
                var nb = new PortBuffer(buf.Capacity);
                foreach (var pkt in buf.Snapshot())
                    nb.TryEnqueue(pkt.Clone());
                nm.InputPorts[p] = nb;
            }
            foreach (var (p, buf) in m.OutputPorts)
            {
                var nb = new PortBuffer(buf.Capacity);
                foreach (var pkt in buf.Snapshot())
                    nb.TryEnqueue(pkt.Clone());
                nm.OutputPorts[p] = nb;
            }
            copy.Machines[id] = nm;
        }
        return copy;
    }

    private static TankInternalState? CloneTank(TankInternalState? tank)
    {
        if (tank == null)
            return null;
        var copy = new TankInternalState { Capacity = tank.Capacity };
        foreach (var p in tank.Storage)
            copy.Storage.Add(p.Clone());
        return copy;
    }

    private static JunctionInternalState? CloneJunction(JunctionInternalState? junction)
    {
        if (junction == null)
            return null;
        return new JunctionInternalState
        {
            NextOutIndex = junction.NextOutIndex,
            Out1Debt = junction.Out1Debt,
            Out2Debt = junction.Out2Debt
        };
    }

    private static ProcessingSlotState? CloneProcessingSlot(ProcessingSlotState? slot)
    {
        if (slot == null)
            return null;
        return new ProcessingSlotState
        {
            Packet = slot.Packet?.Clone(),
            ElapsedTicks = slot.ElapsedTicks,
            TotalTicks = slot.TotalTicks,
            OperationRatePermille = slot.OperationRatePermille,
            TotalDelta = slot.TotalDelta,
            ProcessKind = slot.ProcessKind
        };
    }
}
