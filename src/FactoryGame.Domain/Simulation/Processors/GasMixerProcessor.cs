namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class GasMixerProcessor : IMachineProcessor
{
    public string MachineType => "GasMixer";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var inBudget = ctx.GetPortInputBudget(MachineType, "in1", settingsJson);
        var in2Budget = ctx.GetPortInputBudget(MachineType, "in2", settingsJson);
        var pullBudget = Math.Min(inBudget, in2Budget);

        var a = FlowHelper.PullFromInputBudget(machine, "in1", pullBudget);
        var b = FlowHelper.PullFromInputBudget(machine, "in2", pullBudget);
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

        var ratioA = ResolveRatioPermille(settingsJson);
        var outDna = DnaTransforms.MixGas(a.Dna, b.Dna, ratioA);

        var consumed = Math.Min(a.Quantity, b.Quantity);
        var outBudget = ctx.GetPortOutputBudget(MachineType, "out", settingsJson);
        var outQty = Math.Min(consumed, outBudget);
        if (outQty <= 0)
        {
            machine.GetOrCreateInput("in1").TryEnqueue(a);
            machine.GetOrCreateInput("in2").TryEnqueue(b);
            return;
        }

        a.Quantity = outQty;
        b.Quantity = outQty;
        var dominantElement = ratioA >= 500 ? a.ElementId : b.ElementId;

        var outPkt = new MaterialPacket
        {
            ElementId = dominantElement,
            Dna = outDna,
            Quantity = outQty,
            Quality = MaterialQuality.Normal
        };

        if (!FlowHelper.TryPushOutputBudget(machine, "out", outPkt, outBudget))
        {
            machine.GetOrCreateInput("in1").TryEnqueue(a);
            machine.GetOrCreateInput("in2").TryEnqueue(b);
        }
    }

    private static int ResolveRatioPermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 500, 100, 900, "ratioPermille", "ratio");
}
