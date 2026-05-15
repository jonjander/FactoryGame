namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class UnsupportedProcessor : IMachineProcessor
{
    private readonly string _machineType;

    public UnsupportedProcessor(string machineType) => _machineType = machineType;

    public string MachineType => _machineType;

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson) =>
        machine.BlockedReason ??= $"Machine type {_machineType} is not yet simulated.";
}
