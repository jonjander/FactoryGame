namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class BoilerProcessor : PassThroughProcessor
{
    public override string MachineType => "Boiler";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override long TransformDna(long dna, string? settingsJson)
    {
        var delta = MachineSettingsJson.ReadInt(settingsJson, 8, 4, 32, "heatDelta", "heat", "power");
        return DnaTransforms.Heat(dna, delta);
    }
}
