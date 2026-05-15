namespace FactoryGame.Domain.Simulation;

public sealed class BoardLineState
{
    public const int RuleVersion = 1;

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
            var nm = new MachineRuntimeState { MachineId = m.MachineId, MachineType = m.MachineType, BlockedReason = m.BlockedReason };
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
}
