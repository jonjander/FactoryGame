using System.Text.Json;
using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class SeaportConnectorProcessor : IMachineProcessor
{
    public string MachineType => "SeaportConnector";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (ctx.Pool == null)
            return;

        var elementId = ParseOutElementId(settingsJson);
        var materialDna = ResolveOutMaterialDna(settingsJson, elementId);
        if (elementId > 0 && materialDna != 0)
        {
            if (ctx.Pool.TryWithdraw(elementId, materialDna, ctx.UnitsPerTick))
            {
                var pkt = new MaterialPacket
                {
                    ElementId = elementId,
                    Dna = materialDna,
                    Quantity = ctx.UnitsPerTick
                };
                if (machine.GetOrCreateOutput("out").TryEnqueue(pkt))
                {
                    ctx.SeaportDelta.AddWithdraw(elementId, ctx.UnitsPerTick);
                }
                else if (!ctx.Pool.TryDeposit(elementId, materialDna, ctx.UnitsPerTick))
                {
                    machine.BlockedReason = "Seaport output blocked; pool rollback failed.";
                }
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
            return 0;
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
        return 0;
    }

    internal static long ParseOutMaterialDna(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return 0;
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("outMaterialDna", out var el))
            {
                var dna = MachineSettingsJson.ReadInt64(el);
                if (dna != 0)
                    return dna;
            }
        }
        catch
        {
            /* default */
        }
        return 0;
    }

    internal static long ResolveOutMaterialDna(string? settingsJson, int elementId)
    {
        if (elementId <= 0)
            return 0;
        var explicitDna = ParseOutMaterialDna(settingsJson);
        if (explicitDna != 0)
            return explicitDna;
        return ElementCatalogLookup.CatalogDnaFor(elementId);
    }
}
