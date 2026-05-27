namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class CondenserProcessor : PassThroughProcessor
{
    public override string MachineType => "Condenser";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override string ProcessKind => "condense";

    protected override int ResolveTotalDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 12, 8, 32, "condenseDelta", "coolDelta", "power");
}
