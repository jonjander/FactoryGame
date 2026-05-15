using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Simulation;

internal static class FlowHelper
{
    public static void PushToOutput(MachineRuntimeState machine, string outPort, MaterialPacket packet)
    {
        machine.GetOrCreateOutput(outPort).TryEnqueue(packet);
    }

    public static MaterialPacket? PullFromInput(MachineRuntimeState machine, string inPort) =>
        machine.GetOrCreateInput(inPort).TryDequeue();

    public static bool TryMovePortToPort(
        PortBuffer from,
        PortBuffer to,
        decimal maxQty)
    {
        var pkt = from.Peek();
        if (pkt == null || to.IsFull)
            return false;
        var moved = from.TryDequeue();
        if (moved == null)
            return false;
        var qty = Math.Min(moved.Quantity, maxQty);
        moved.Quantity = qty;
        if (!to.TryEnqueue(moved))
        {
            from.TryEnqueue(moved);
            return false;
        }
        return true;
    }

    public static void InitPortsForMachine(MachineRuntimeState machine, string machineType)
    {
        foreach (var p in MachinePortCatalog.GetPorts(machineType))
        {
            if (p.Direction == PortDirection.In)
                _ = machine.GetOrCreateInput(p.Name);
            else
                _ = machine.GetOrCreateOutput(p.Name);
        }
    }

    public static string? CheckDnaBlock(string machineType, MaterialPacket? packet)
    {
        if (packet == null)
            return null;
        return MachineDnaCompatibility.GetIncompatibilityReason(machineType, packet.Dna);
    }
}
