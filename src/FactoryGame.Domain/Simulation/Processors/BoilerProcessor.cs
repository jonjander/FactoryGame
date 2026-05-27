namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class BoilerProcessor : PassThroughProcessor
{
    public override string MachineType => "Boiler";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override string ProcessKind => "heat";

    protected override int ResolveTotalDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 8, 4, 32, "heatDelta", "heat", "power");
}
