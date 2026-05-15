using System.Text.Json;
using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class MelterProcessor : IMachineProcessor
{
    private const int MinSpreadPermille = 220;

    public string MachineType => "Melter";

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

        var cut = ResolveCutBoil(settingsJson);
        var heat = ResolveHeatDelta(settingsJson);
        var (outDna, _) = DnaTransforms.Melt(pkt.Dna, cut, heat);

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

    private static int ResolveCutBoil(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.BoilingMask,
            "cutBoiling", "cutPoint", "cut", "targetBoil");

    private static int ResolveHeatDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 20, 8, 128, "heatDelta", "heat", "power");
}
