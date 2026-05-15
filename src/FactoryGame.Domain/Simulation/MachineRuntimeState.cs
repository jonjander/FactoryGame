namespace FactoryGame.Domain.Simulation;

public sealed class MachineRuntimeState
{
    public required string MachineId { get; init; }
    public required string MachineType { get; init; }
    public string? BlockedReason { get; set; }
    public Dictionary<string, PortBuffer> InputPorts { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, PortBuffer> OutputPorts { get; } = new(StringComparer.Ordinal);

    public bool IsBlocked => BlockedReason != null;

    public PortBuffer GetOrCreateInput(string portName)
    {
        if (!InputPorts.TryGetValue(portName, out var buf))
        {
            buf = new PortBuffer();
            InputPorts[portName] = buf;
        }
        return buf;
    }

    public PortBuffer GetOrCreateOutput(string portName)
    {
        if (!OutputPorts.TryGetValue(portName, out var buf))
        {
            buf = new PortBuffer();
            OutputPorts[portName] = buf;
        }
        return buf;
    }
}
