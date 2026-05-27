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

        var slot = machine.ProcessingSlot;
        if (slot?.Packet != null)
        {
            AdvanceSpreadProcessing(machine, ctx, settingsJson, slot);
            return;
        }

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

        var spread = DnaTransforms.MeasureDnaSpreadPermille(pkt.Dna);
        if (spread < MinSpreadPermille)
        {
            Passthrough(machine, ctx, settingsJson, pkt);
            return;
        }

        var heat = ResolveHeatDelta(settingsJson);
        slot = ProcessTimingDna.EnsureSlot(machine);
        slot.Packet = pkt;
        slot.ElapsedTicks = 0;
        slot.TotalTicks = ProcessTimingDna.ResolveTotalTicks(heat, settingsJson);
        slot.OperationRatePermille = MachineRateCatalog.GetOperationRatePermille(settingsJson);
        slot.TotalDelta = heat;
        slot.ProcessKind = "melt";
        AdvanceSpreadProcessing(machine, ctx, settingsJson, slot);
    }

    private static void AdvanceSpreadProcessing(
        MachineRuntimeState machine,
        TickContext ctx,
        string? settingsJson,
        ProcessingSlotState slot)
    {
        var pkt = slot.Packet!;
        slot.ElapsedTicks++;

        if (slot.ElapsedTicks < slot.TotalTicks)
            return;

        var cut = ResolveCutBoil(settingsJson);
        var heat = ResolveHeatDelta(settingsJson);
        var (outDna, _) = DnaTransforms.Melt(pkt.Dna, cut, heat);

        var outPkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = outDna,
            Quantity = pkt.Quantity,
            Quality = pkt.Quality
        };

        var outBudget = ctx.GetPortOutputBudget("Melter", "out", settingsJson);
        if (FlowHelper.TryPushOutputBudget(machine, "out", outPkt, outBudget))
        {
            slot.Packet = null;
            slot.ElapsedTicks = 0;
            return;
        }

        slot.ElapsedTicks--;
    }

    private static void Passthrough(
        MachineRuntimeState machine,
        TickContext ctx,
        string? settingsJson,
        MaterialPacket pkt)
    {
        var outBudget = ctx.GetPortOutputBudget("Melter", "out", settingsJson);
        var pass = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = pkt.Dna,
            Quantity = Math.Min(pkt.Quantity, outBudget),
            Quality = pkt.Quality
        };
        if (!FlowHelper.TryPushOutputBudget(machine, "out", pass, outBudget))
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
    }

    private static int ResolveCutBoil(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.BoilingMask,
            "cutBoiling", "cutPoint", "cut", "targetBoil");

    private static int ResolveHeatDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 20, 8, 128, "heatDelta", "heat", "power");
}
