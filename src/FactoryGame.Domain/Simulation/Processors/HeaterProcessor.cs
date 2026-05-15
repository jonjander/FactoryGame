namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class HeaterProcessor : PassThroughProcessor
{
    public override string MachineType => "Heater";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override long TransformDna(long dna) => DnaTransforms.Heat(dna, 4);
}
