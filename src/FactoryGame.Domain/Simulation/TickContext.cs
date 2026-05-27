namespace FactoryGame.Domain.Simulation;

public sealed class TickContext
{
    public required long Tick { get; init; }
    public required decimal UnitsPerTick { get; init; }
    public SimulationPlan? Plan { get; init; }
    public ISeaportPoolSink? Pool { get; init; }
    public SeaportTickDelta SeaportDelta { get; } = new();

    public decimal GetEffectiveRate(string machineType, string? settingsJson) =>
        MachineRateCatalog.GetEffectiveRateUnits(machineType, settingsJson, UnitsPerTick);

    public decimal GetPortInputBudget(string machineType, string portName, string? settingsJson) =>
        MachineRateCatalog.GetPortInputBudget(machineType, portName, settingsJson, UnitsPerTick);

    public decimal GetPortOutputBudget(string machineType, string portName, string? settingsJson) =>
        MachineRateCatalog.GetPortOutputBudget(machineType, portName, settingsJson, UnitsPerTick);
}
