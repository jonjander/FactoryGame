using System.Text.Json;
using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class LiquidSeparatorProcessor : IMachineProcessor
{
    private const int MinSpreadPermille = 220;
    private const int FullSpreadPermille = 650;

    public string MachineType => "LiquidSeparator";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var pkt = FlowHelper.PullFromInput(machine, "in");
        if (pkt == null)
            return;

        var block = FlowHelper.CheckDnaBlock(machine.MachineType, pkt);
        if (block != null)
        {
            machine.BlockedReason = block;
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
            return;
        }

        var spread = DnaTransforms.MeasureDnaSpreadPermille(pkt.Dna);
        var splitStrength = ComputeSplitStrengthPermille(spread);
        var total = Math.Min(pkt.Quantity, ctx.UnitsPerTick);

        if (splitStrength == 0)
        {
            PassthroughDense(machine, pkt, total);
            return;
        }

        var decoded = DnaDecoder.Decode(pkt.Dna);
        var cut = ResolveCutFreeze(settingsJson);
        var (denseDna, lightDna) = DnaTransforms.LiquidSeparateFractions(pkt.Dna, cut);

        var densePermille = ComputeDensePermille(decoded.FreezePoint, cut, splitStrength);
        var denseQty = ClampSplit(total * densePermille / 1000m, total, splitStrength);
        var lightQty = total - denseQty;

        var densePkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = denseDna,
            Quantity = denseQty,
            Quality = pkt.Quality
        };
        var lightPkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = lightDna,
            Quantity = lightQty,
            Quality = pkt.Quality
        };

        var outDense = machine.GetOrCreateOutput("out1");
        var outLight = machine.GetOrCreateOutput("out2");

        if (!outDense.TryEnqueue(densePkt))
        {
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
            return;
        }

        if (!outLight.TryEnqueue(lightPkt))
        {
            _ = outDense.TryDequeue();
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
        }
    }

    private static void PassthroughDense(MachineRuntimeState machine, MaterialPacket pkt, decimal qty)
    {
        var pass = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = pkt.Dna,
            Quantity = qty,
            Quality = pkt.Quality
        };
        if (!machine.GetOrCreateOutput("out1").TryEnqueue(pass))
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
    }

    private static int ComputeSplitStrengthPermille(int spreadPermille)
    {
        if (spreadPermille < MinSpreadPermille)
            return 0;
        return Math.Clamp(
            (spreadPermille - MinSpreadPermille) * 1000 / (FullSpreadPermille - MinSpreadPermille),
            50,
            1000);
    }

    private static decimal ClampSplit(decimal denseQty, decimal total, int splitStrengthPermille)
    {
        if (total <= 0)
            return 0;
        var minSide = total * splitStrengthPermille * 0.15m / 1000m;
        minSide = Math.Max(minSide, total * 0.05m);
        var maxDense = total - minSide;
        return Math.Clamp(denseQty, minSide, maxDense);
    }

    private static int ComputeDensePermille(int freezePoint, int cut, int splitStrengthPermille)
    {
        var span = Math.Max(cut, 1);
        var aboveCut = Math.Clamp((freezePoint - cut) * 500 / span + 500, 200, 800);
        return Math.Clamp(aboveCut * splitStrengthPermille / 1000, 200, 800);
    }

    private static int ResolveCutFreeze(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.FreezeMask,
            "cutFreeze", "cutPoint", "cut");
}
