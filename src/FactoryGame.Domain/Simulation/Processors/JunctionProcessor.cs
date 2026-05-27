namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class JunctionProcessor : IMachineProcessor
{
    public string MachineType => "Junction";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var junction = machine.Junction ??= new JunctionInternalState();
        var inBudget = ctx.GetPortInputBudget(MachineType, "in", settingsJson);
        var pkt = FlowHelper.PullFromInputBudget(machine, "in", inBudget);
        if (pkt == null)
            return;

        var out1Cap = ResolveOutputCapacity(machine, ctx, "out1");
        var out2Cap = ResolveOutputCapacity(machine, ctx, "out2");
        var canOut1 = FlowHelper.CanOutputAccept(machine, "out1") && out1Cap > 0;
        var canOut2 = FlowHelper.CanOutputAccept(machine, "out2") && out2Cap > 0;

        if (!canOut1 && !canOut2)
        {
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
            return;
        }

        var (out1Qty, out2Qty) = Allocate(pkt.Quantity, canOut1, canOut2, out1Cap, out2Cap, junction);
        var sentAny = false;

        if (out1Qty > 0)
        {
            var p1 = pkt.Clone();
            p1.Quantity = out1Qty;
            if (FlowHelper.TryPushOutputBudget(machine, "out1", p1, out1Qty))
                sentAny = true;
            else
                out1Qty = 0;
        }

        if (out2Qty > 0)
        {
            var p2 = pkt.Clone();
            p2.Quantity = out2Qty;
            if (FlowHelper.TryPushOutputBudget(machine, "out2", p2, out2Qty))
                sentAny = true;
            else
                out2Qty = 0;
        }

        var remainder = pkt.Quantity - out1Qty - out2Qty;
        if (remainder > 0 || !sentAny)
        {
            var putBack = remainder > 0 ? remainder : pkt.Quantity;
            if (putBack > 0)
            {
                var back = pkt.Clone();
                back.Quantity = putBack;
                machine.GetOrCreateInput("in").TryEnqueue(back);
            }
        }
    }

    private static decimal ResolveOutputCapacity(MachineRuntimeState machine, TickContext ctx, string outPort)
    {
        if (ctx.Plan == null)
            return ctx.GetPortOutputBudget("Junction", outPort, null);

        var downstream = MachineRateCatalog.GetDownstreamInputCapacity(ctx.Plan, machine.MachineId, outPort, ctx.UnitsPerTick);
        var local = ctx.GetPortOutputBudget("Junction", outPort, null);
        return downstream > 0 ? Math.Min(local, downstream) : local;
    }

    private static (decimal Out1Qty, decimal Out2Qty) Allocate(
        decimal quantity,
        bool canOut1,
        bool canOut2,
        decimal out1Cap,
        decimal out2Cap,
        JunctionInternalState junction)
    {
        if (quantity <= 0)
            return (0, 0);

        if (!canOut1)
            return (0, Math.Min(quantity, out2Cap));
        if (!canOut2)
            return (Math.Min(quantity, out1Cap), 0);

        if (Math.Abs(out1Cap - out2Cap) < 0.0001m && quantity <= 1m)
        {
            if (junction.NextOutIndex == 0)
            {
                junction.NextOutIndex = 1;
                return (Math.Min(quantity, out1Cap), 0);
            }

            junction.NextOutIndex = 0;
            return (0, Math.Min(quantity, out2Cap));
        }

        var totalCap = out1Cap + out2Cap;
        if (totalCap <= 0)
            return (0, 0);

        junction.Out1Debt += quantity * out1Cap / totalCap;
        junction.Out2Debt += quantity * out2Cap / totalCap;

        var out1Qty = Math.Min(Math.Floor(junction.Out1Debt), Math.Min(quantity, out1Cap));
        junction.Out1Debt -= out1Qty;
        var remaining = quantity - out1Qty;
        var out2Qty = Math.Min(Math.Floor(junction.Out2Debt), Math.Min(remaining, out2Cap));
        junction.Out2Debt -= out2Qty;

        return (out1Qty, out2Qty);
    }
}
