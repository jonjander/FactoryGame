namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class CoolerProcessor : PassThroughProcessor
{
    public override string MachineType => "Cooler";
    protected override string InPort => "in";
    protected override string OutPort => "out";
    protected override long TransformDna(long dna) => DnaTransforms.Cool(dna);
}
