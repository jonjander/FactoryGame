namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class HeaterProcessor : PassThroughProcessor
{
    public override string MachineType => "Heater";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override string ProcessKind => "heat";

    protected override int ResolveTotalDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 8, 4, 32, "heatDelta", "heat", "power");
}
