using System.Text.Json;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class SorterProcessor : IMachineProcessor
{
    public string MachineType => "Sorter";

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

        var outPort = ResolveOutPort(pkt.ElementId, settingsJson);
        pkt.Quantity = Math.Min(pkt.Quantity, ctx.UnitsPerTick);
        if (!machine.GetOrCreateOutput(outPort).TryEnqueue(pkt))
            machine.GetOrCreateInput("in").TryEnqueue(pkt);
    }

    private static string ResolveOutPort(int elementId, string? settingsJson)
    {
        if (!string.IsNullOrEmpty(settingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                var root = doc.RootElement;
                foreach (var (portKey, outName) in new[] { ("port1", "out1"), ("port2", "out2"), ("port3", "out3") })
                {
                    if (root.TryGetProperty(portKey, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id) && id == elementId)
                                return outName;
                        }
                    }
                }
            }
            catch
            {
                /* use rest port */
            }
        }

        return "out4";
    }
}
