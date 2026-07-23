namespace FactoryGame.Domain.Simulation.Processors;

/// <summary>Controlled flare — consumes burnable gas completely (no output).</summary>
internal sealed class BurnerProcessor : IMachineProcessor
{
    public string MachineType => "Burner";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var budget = ctx.GetPortInputBudget(MachineType, "in", settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, "in", budget);
        if (pkt == null)
            return;

        var block = FlowHelper.CheckDnaBlock(machine.MachineType, pkt);
        if (block != null)
        {
            machine.BlockedReason = block;
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
            return;
        }

        machine.BlockedReason = null;
    }
}
