namespace FactoryGame.Domain.Simulation.Processors;

internal abstract class PassThroughProcessor : IMachineProcessor
{
    public abstract string MachineType { get; }
    protected abstract string InPort { get; }
    protected abstract string OutPort { get; }
    protected abstract long TransformDna(long dna, string? settingsJson);

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var pkt = FlowHelper.PullFromInput(machine, InPort);
        if (pkt == null)
            return;

        var block = FlowHelper.CheckDnaBlock(machine.MachineType, pkt);
        if (block != null)
        {
            machine.BlockedReason = block;
            machine.GetOrCreateInput(InPort).TryEnqueue(pkt);
            return;
        }

        pkt.Dna = TransformDna(pkt.Dna, settingsJson);
        pkt.Quantity = Math.Min(pkt.Quantity, ctx.UnitsPerTick);
        if (!machine.GetOrCreateOutput(OutPort).TryEnqueue(pkt))
            machine.GetOrCreateInput(InPort).TryEnqueue(pkt);
    }
}
