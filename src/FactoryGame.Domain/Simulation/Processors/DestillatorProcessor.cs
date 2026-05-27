using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class DestillatorProcessor : IMachineProcessor
{
    public string MachineType => "Destilator";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var inBudget = ctx.GetPortInputBudget(MachineType, "in", settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, "in", inBudget);
        if (pkt == null)
            return;

        var block = FlowHelper.CheckDnaBlock(machine.MachineType, pkt);
        if (block != null)
        {
            machine.BlockedReason = block;
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
            return;
        }

        var decoded = DnaDecoder.Decode(pkt.Dna);
        var cut = ResolveCutBoiling(settingsJson);
        var (heavyDna, lightDna) = DnaTransforms.DistillFractions(pkt.Dna, cut);

        var total = pkt.Quantity;
        var heavyPermille = ComputeHeavyPermille(decoded.BoilingPoint, cut, settingsJson);
        var heavyQty = total * heavyPermille / 1000m;
        heavyQty = ClampSplit(heavyQty, total);
        var lightQty = total - heavyQty;

        var heavyBudget = ctx.GetPortOutputBudget(MachineType, "out1", settingsJson);
        var lightBudget = ctx.GetPortOutputBudget(MachineType, "out2", settingsJson);
        heavyQty = Math.Min(heavyQty, heavyBudget);
        lightQty = Math.Min(lightQty, lightBudget);

        var heavyPkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = heavyDna,
            Quantity = heavyQty,
            Quality = pkt.Quality
        };
        var lightPkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = lightDna,
            Quantity = lightQty,
            Quality = pkt.Quality
        };

        var outHeavy = machine.GetOrCreateOutput("out1");
        var outLight = machine.GetOrCreateOutput("out2");

        if (heavyQty > 0 && !outHeavy.TryEnqueue(heavyPkt))
        {
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
            return;
        }

        if (lightQty > 0 && !outLight.TryEnqueue(lightPkt))
        {
            if (heavyQty > 0)
                _ = outHeavy.TryDequeue();
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
        }
    }

    private static decimal ClampSplit(decimal heavyQty, decimal total)
    {
        if (total <= 0)
            return 0;
        var minSide = total * 0.15m;
        var maxHeavy = total - minSide;
        return Math.Clamp(heavyQty, minSide, maxHeavy);
    }

    private static int ComputeHeavyPermille(int boilingPoint, int cut, string? settingsJson)
    {
        var reflux = ResolveRefluxPermille(settingsJson);
        var span = Math.Max(cut, 1);
        var aboveCut = Math.Clamp((boilingPoint - cut) * 500 / span + 500, 150, 850);
        var withReflux = Math.Clamp(aboveCut + reflux / 5, 150, 850);
        return withReflux;
    }

    private static int ResolveCutBoiling(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.BoilingMask,
            "cutBoiling", "cutPoint", "cut");

    private static int ResolveRefluxPermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 0, 0, 500, "refluxPermille", "reflux");
}
