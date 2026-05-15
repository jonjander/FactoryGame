namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class MixerProcessor : IMachineProcessor
{
    public string MachineType => "Mixer";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var a = FlowHelper.PullFromInput(machine, "in1");
        var b = FlowHelper.PullFromInput(machine, "in2");
        if (a == null || b == null)
        {
            if (a != null) machine.GetOrCreateInput("in1").TryEnqueue(a);
            if (b != null) machine.GetOrCreateInput("in2").TryEnqueue(b);
            return;
        }

        foreach (var pkt in new[] { a, b })
        {
            var block = FlowHelper.CheckDnaBlock(machine.MachineType, pkt);
            if (block != null)
            {
                machine.BlockedReason = block;
                machine.GetOrCreateInput("in1").TryEnqueue(a);
                machine.GetOrCreateInput("in2").TryEnqueue(b);
                return;
            }
        }

        var outPkt = new MaterialPacket
        {
            ElementId = a.ElementId,
            Dna = DnaTransforms.Mix(a.Dna, b.Dna),
            Quantity = Math.Min(Math.Min(a.Quantity, b.Quantity), ctx.UnitsPerTick)
        };
        if (!machine.GetOrCreateOutput("out").TryEnqueue(outPkt))
        {
            machine.GetOrCreateInput("in1").TryEnqueue(a);
            machine.GetOrCreateInput("in2").TryEnqueue(b);
        }
    }
}
