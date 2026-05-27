namespace FactoryGame.Domain.Simulation.Processors;

internal abstract class PassThroughProcessor : IMachineProcessor
{
    public abstract string MachineType { get; }
    protected abstract string InPort { get; }
    protected abstract string OutPort { get; }
    protected abstract string ProcessKind { get; }
    protected abstract int ResolveTotalDelta(string? settingsJson);

    protected virtual long TransformDna(long dna, string? settingsJson) =>
        ProcessTimingDna.ApplyPartialTransform(ProcessKind, dna, ResolveTotalDelta(settingsJson), settingsJson);

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var profile = MachineRateCatalog.GetProfile(MachineType);
        if (!profile.SupportsTimeDna)
        {
            ProcessInstant(machine, ctx, settingsJson);
            return;
        }

        ProcessTimed(machine, ctx, settingsJson);
    }

    private void ProcessInstant(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        var budget = ctx.GetPortInputBudget(MachineType, InPort, settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, InPort, budget);
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
        var outBudget = ctx.GetPortOutputBudget(MachineType, OutPort, settingsJson);
        if (!FlowHelper.TryPushOutputBudget(machine, OutPort, pkt, outBudget))
            machine.GetOrCreateInput(InPort).TryEnqueue(pkt);
    }

    private void ProcessTimed(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        var slot = machine.ProcessingSlot;
        if (slot?.Packet != null)
        {
            AdvanceSlot(machine, ctx, settingsJson, slot);
            return;
        }

        var budget = ctx.GetPortInputBudget(MachineType, InPort, settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, InPort, budget);
        if (pkt == null)
            return;

        var block = FlowHelper.CheckDnaBlock(machine.MachineType, pkt);
        if (block != null)
        {
            machine.BlockedReason = block;
            machine.GetOrCreateInput(InPort).TryEnqueue(pkt);
            return;
        }

        var totalDelta = ResolveTotalDelta(settingsJson);
        var opRate = MachineRateCatalog.GetOperationRatePermille(settingsJson);
        slot = ProcessTimingDna.EnsureSlot(machine);
        slot.Packet = pkt;
        slot.ElapsedTicks = 0;
        slot.TotalTicks = ProcessTimingDna.ResolveTotalTicks(totalDelta, settingsJson);
        slot.OperationRatePermille = opRate;
        slot.TotalDelta = totalDelta;
        slot.ProcessKind = ProcessKind;
        AdvanceSlot(machine, ctx, settingsJson, slot);
    }

    private void AdvanceSlot(
        MachineRuntimeState machine,
        TickContext ctx,
        string? settingsJson,
        ProcessingSlotState slot)
    {
        var pkt = slot.Packet!;
        var dnaBefore = pkt.Dna;
        slot.ElapsedTicks++;
        var partial = ProcessTimingDna.ResolvePartialDelta(slot.TotalDelta, slot.TotalTicks, slot.ElapsedTicks);
        pkt.Dna = ProcessTimingDna.ApplyPartialTransform(slot.ProcessKind, pkt.Dna, partial, settingsJson);

        if (slot.ElapsedTicks < slot.TotalTicks)
            return;

        pkt.Quality = ProcessTimingDna.ResolveCompletionQuality(
            slot.OperationRatePermille, slot.TotalDelta, dnaBefore, pkt.Dna, slot.ProcessKind);

        var outBudget = ctx.GetPortOutputBudget(MachineType, OutPort, settingsJson);
        if (FlowHelper.TryPushOutputBudget(machine, OutPort, pkt, outBudget))
        {
            slot.Packet = null;
            slot.ElapsedTicks = 0;
            return;
        }

        slot.ElapsedTicks--;
        pkt.Dna = dnaBefore;
    }
}
