namespace FactoryGame.Domain.Simulation;

public sealed class TickContext
{
    public required long Tick { get; init; }
    public required decimal UnitsPerTick { get; init; }
    public ISeaportPoolSink? Pool { get; init; }
    public SeaportTickDelta SeaportDelta { get; } = new();
}
