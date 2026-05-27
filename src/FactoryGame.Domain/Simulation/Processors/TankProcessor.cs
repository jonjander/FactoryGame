namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class TankProcessor : IMachineProcessor
{
    public string MachineType => "Tank";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var tank = machine.Tank ??= new TankInternalState
        {
            Capacity = MachineRateCatalog.GetTankCapacity(settingsJson)
        };
        tank.Capacity = MachineRateCatalog.GetTankCapacity(settingsJson);

        var inBudget = ctx.GetPortInputBudget(MachineType, "in", settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, "in", inBudget);
        if (pkt != null)
        {
            if (!FlowHelper.TryTankStore(tank, pkt))
                machine.GetOrCreateInput("in").TryEnqueue(pkt);
            else if (pkt.Quantity > 0)
                machine.GetOrCreateInput("in").TryEnqueue(pkt);
        }

        var outBudget = ctx.GetPortOutputBudget(MachineType, "out", settingsJson);
        if (outBudget <= 0 || machine.GetOrCreateOutput("out").IsFull)
            return;

        var withdrawn = FlowHelper.TryTankWithdraw(tank, outBudget);
        if (withdrawn == null)
            return;

        if (!FlowHelper.TryPushOutputBudget(machine, "out", withdrawn, outBudget))
        {
            FlowHelper.TryTankStore(tank, withdrawn);
            if (withdrawn.Quantity > 0)
            {
                var head = tank.Storage.FirstOrDefault();
                if (head != null && head.ElementId == withdrawn.ElementId && head.Dna == withdrawn.Dna)
                    head.Quantity += withdrawn.Quantity;
                else
                    tank.Storage.Insert(0, withdrawn);
            }
        }
    }
}
