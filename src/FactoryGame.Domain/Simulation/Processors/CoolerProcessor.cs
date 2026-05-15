namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class CoolerProcessor : PassThroughProcessor
{
    public override string MachineType => "Cooler";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override long TransformDna(long dna, string? settingsJson)
    {
        var delta = MachineSettingsJson.ReadInt(settingsJson, 8, 4, 32, "coolDelta", "cool", "power");
        return DnaTransforms.Cool(dna, delta);
    }
}
