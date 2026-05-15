using System.Text.Json;
using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class CrystallizerProcessor : IMachineProcessor
{
    private const int MinSpreadPermille = 220;

    public string MachineType => "Crystallizer";

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

        var total = Math.Min(pkt.Quantity, ctx.UnitsPerTick);
        var spread = DnaTransforms.MeasureDnaSpreadPermille(pkt.Dna);
        if (spread < MinSpreadPermille)
        {
            Passthrough(machine, pkt, total);
            return;
        }

        var cut = ResolveCutFreeze(settingsJson);
        var chill = ResolveChillDelta(settingsJson);
        var (outDna, _) = DnaTransforms.Crystallize(pkt.Dna, cut, chill);

        var outPkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = outDna,
            Quantity = total,
            Quality = pkt.Quality
        };

        if (!machine.GetOrCreateOutput("out").TryEnqueue(outPkt))
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
    }

    private static void Passthrough(MachineRuntimeState machine, MaterialPacket pkt, decimal qty)
    {
        var pass = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = pkt.Dna,
            Quantity = qty,
            Quality = pkt.Quality
        };
        if (!machine.GetOrCreateOutput("out").TryEnqueue(pass))
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
    }

    private static int ResolveCutFreeze(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.FreezeMask,
            "cutFreeze", "cutPoint", "cut", "targetFreeze");

    private static int ResolveChillDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 16, 4, 128, "chillDelta", "chill", "coolingPower");
}
