namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class CondenserProcessor : PassThroughProcessor
{
    public override string MachineType => "Condenser";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override long TransformDna(long dna, string? settingsJson)
    {
        var delta = MachineSettingsJson.ReadInt(settingsJson, 12, 8, 32, "condenseDelta", "coolDelta", "power");
        return DnaTransforms.Condense(dna, delta);
    }
}
