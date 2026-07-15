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

        var decoded = DnaDecoder.Decode(pkt.Dna);
        if (decoded.Phase == MaterialPhase.Solid)
        {
            Passthrough(machine, ctx, settingsJson, pkt);
            return;
        }

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

        var chill = ResolveChillDelta(settingsJson);
        slot = ProcessTimingDna.EnsureSlot(machine);
        slot.Packet = pkt;
        slot.ElapsedTicks = 0;
        slot.TotalTicks = ProcessTimingDna.ResolveTotalTicks(chill, settingsJson);
        slot.OperationRatePermille = MachineRateCatalog.GetOperationRatePermille(settingsJson);
        slot.TotalDelta = chill;
        slot.ProcessKind = "crystallize";
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

        var cut = ResolveCutFreeze(settingsJson);
        var chill = ResolveChillDelta(settingsJson);
        var (outDna, crystallized) = DnaTransforms.Crystallize(pkt.Dna, cut, chill);

        if (!crystallized)
        {
            pkt.Dna = outDna;
            slot.ElapsedTicks = 0;
            return;
        }

        var outPkt = new MaterialPacket
        {
            ElementId = pkt.ElementId,
            Dna = outDna,
            Quantity = pkt.Quantity,
            Quality = pkt.Quality
        };

        var outBudget = ctx.GetPortOutputBudget("Crystallizer", "out", settingsJson);
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
        var outBudget = ctx.GetPortOutputBudget("Crystallizer", "out", settingsJson);
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

    private static int ResolveCutFreeze(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.FreezeMask,
            "cutFreeze", "cutPoint", "cut", "targetFreeze");

    private static int ResolveChillDelta(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 16, 4, 128, "chillDelta", "chill", "coolingPower");
}
