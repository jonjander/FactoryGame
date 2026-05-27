namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class CoolerProcessor : PassThroughProcessor
{
    public override string MachineType => "Cooler";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override string ProcessKind => "cool";

    protected override int ResolveTotalDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 8, 4, 32, "coolDelta", "cool", "power");
}
