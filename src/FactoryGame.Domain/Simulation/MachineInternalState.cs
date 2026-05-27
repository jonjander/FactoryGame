namespace FactoryGame.Domain.Simulation;

public sealed class TankInternalState
{
    public List<MaterialPacket> Storage { get; } = [];
    public int Capacity { get; set; } = 24;
    public decimal StoredQuantity => Storage.Sum(p => p.Quantity);
}

public sealed class JunctionInternalState
{
    public int NextOutIndex { get; set; }
    public decimal Out1Debt { get; set; }
    public decimal Out2Debt { get; set; }
}

public sealed class ProcessingSlotState
{
    public MaterialPacket? Packet { get; set; }
    public int ElapsedTicks { get; set; }
    public int TotalTicks { get; set; } = 1;
    public int OperationRatePermille { get; set; } = MachineRateCatalog.DefaultOperationRatePermille;
    public int TotalDelta { get; set; }
    public string ProcessKind { get; set; } = "";
}
