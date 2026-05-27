namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class RateLimiterProcessor : IMachineProcessor
{
    public string MachineType => "RateLimiter";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var budget = ctx.GetEffectiveRate(MachineType, settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, "in", budget);
        if (pkt == null)
            return;

        if (!FlowHelper.TryPushOutputBudget(machine, "out", pkt, budget))
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
    }
}
