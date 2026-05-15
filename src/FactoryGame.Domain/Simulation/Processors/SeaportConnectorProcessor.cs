using System.Text.Json;
using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class SeaportConnectorProcessor : IMachineProcessor
{
    public string MachineType => "SeaportConnector";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked || ctx.Pool == null)
            return;

        var elementId = ParseOutElementId(settingsJson);
        if (elementId > 0)
        {
            if (ctx.Pool.TryWithdraw(elementId, ctx.UnitsPerTick))
            {
                ctx.SeaportDelta.AddWithdraw(elementId, ctx.UnitsPerTick);
                var element = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId);
                var pkt = new MaterialPacket
                {
                    ElementId = elementId,
                    Dna = element.Id == elementId ? element.Dna : 0,
                    Quantity = ctx.UnitsPerTick
                };
                machine.GetOrCreateOutput("out").TryEnqueue(pkt);
            }
        }

        var incoming = FlowHelper.PullFromInput(machine, "in");
        if (incoming != null)
        {
            if (ctx.Pool.TryDeposit(incoming.ElementId, incoming.Dna, incoming.Quantity))
            {
                ctx.SeaportDelta.AddDeposit(incoming.ElementId, incoming.Quantity);
            }
            else
            {
                machine.BlockedReason = "Seaport pool volume full.";
                machine.GetOrCreateInput("in").TryEnqueue(incoming);
            }
        }
    }

    internal static int ParseOutElementId(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return 1;
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("outElementId", out var el) && el.TryGetInt32(out var id))
                return id;
        }
        catch
        {
            /* default */
        }
        return 1;
    }
}

internal sealed class SeaportInProcessor : IMachineProcessor
{
    public string MachineType => "SeaportIn";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked || ctx.Pool == null)
            return;
        var elementId = SeaportConnectorProcessor.ParseOutElementId(settingsJson);
        if (elementId <= 0)
            return;
        if (!ctx.Pool.TryWithdraw(elementId, ctx.UnitsPerTick))
            return;
        ctx.SeaportDelta.AddWithdraw(elementId, ctx.UnitsPerTick);
        var element = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId);
        var pkt = new MaterialPacket
        {
            ElementId = elementId,
            Dna = element.Id == elementId ? element.Dna : 0,
            Quantity = ctx.UnitsPerTick
        };
        machine.GetOrCreateOutput("out").TryEnqueue(pkt);
    }
}

internal sealed class SeaportOutProcessor : IMachineProcessor
{
    public string MachineType => "SeaportOut";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (ctx.Pool == null)
            return;
        var incoming = FlowHelper.PullFromInput(machine, "in");
        if (incoming == null)
            return;
        if (!ctx.Pool.TryDeposit(incoming.ElementId, incoming.Dna, incoming.Quantity))
        {
            machine.BlockedReason = "Seaport pool volume full.";
            machine.GetOrCreateInput("in").TryEnqueue(incoming);
            return;
        }
        ctx.SeaportDelta.AddDeposit(incoming.ElementId, incoming.Quantity);
    }
}
