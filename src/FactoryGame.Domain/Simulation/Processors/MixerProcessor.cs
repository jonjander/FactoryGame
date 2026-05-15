using System.Text.Json;

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

        var ratioA = ResolveRatioPermille(settingsJson);
        var intensity = ResolveMixIntensity(settingsJson);
        var (outDna, tier) = DnaTransforms.MixCombined(a.Dna, b.Dna, ratioA, intensity);

        var ratioWeight = ratioA / 1000m;
        var outQty = Math.Min(
            Math.Min(a.Quantity * ratioWeight, b.Quantity * (1m - ratioWeight + 0.5m)),
            ctx.UnitsPerTick);
        if (outQty <= 0)
            outQty = Math.Min(Math.Min(a.Quantity, b.Quantity), ctx.UnitsPerTick);

        var dominantElement = ratioA >= 500 ? a.ElementId : b.ElementId;

        var outPkt = new MaterialPacket
        {
            ElementId = dominantElement,
            Dna = outDna,
            Quantity = outQty,
            Quality = tier == MixTier.Poor ? MaterialQuality.Ash : MaterialQuality.Normal
        };

        if (!machine.GetOrCreateOutput("out").TryEnqueue(outPkt))
        {
            machine.GetOrCreateInput("in1").TryEnqueue(a);
            machine.GetOrCreateInput("in2").TryEnqueue(b);
        }
    }

    private static int ResolveRatioPermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 500, 100, 900, "ratioPermille", "ratio");

    private static int ResolveMixIntensity(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 350, 100, 1000,
            "mixIntensityPermille", "mixIntensity", "intensity");
}
