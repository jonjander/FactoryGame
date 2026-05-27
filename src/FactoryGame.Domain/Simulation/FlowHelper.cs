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

    public static MaterialPacket? PullFromInputBudget(
        MachineRuntimeState machine,
        string inPort,
        decimal maxQty)
    {
        var buf = machine.GetOrCreateInput(inPort);
        var pkt = buf.Peek();
        if (pkt == null || maxQty <= 0)
            return null;

        var moved = buf.TryDequeue();
        if (moved == null)
            return null;

        if (moved.Quantity > maxQty)
        {
            var remainder = moved.Clone();
            remainder.Quantity = moved.Quantity - maxQty;
            moved.Quantity = maxQty;
            buf.TryEnqueue(remainder);
        }

        return moved;
    }

    public static bool TryMovePortToPort(
        PortBuffer from,
        PortBuffer to,
        decimal maxQty)
    {
        var pkt = from.Peek();
        if (pkt == null || to.IsFull || maxQty <= 0)
            return false;
        var moved = from.TryDequeue();
        if (moved == null)
            return false;
        var originalQty = moved.Quantity;
        var qty = Math.Min(originalQty, maxQty);
        moved.Quantity = qty;
        if (!to.TryEnqueue(moved))
        {
            moved.Quantity = originalQty;
            from.TryEnqueue(moved);
            return false;
        }

        if (originalQty > qty)
        {
            var remainder = moved.Clone();
            remainder.Quantity = originalQty - qty;
            from.TryEnqueue(remainder);
        }

        return true;
    }

    public static bool TryPushOutputBudget(
        MachineRuntimeState machine,
        string outPort,
        MaterialPacket packet,
        decimal maxQty)
    {
        if (maxQty <= 0)
            return false;
        packet.Quantity = Math.Min(packet.Quantity, maxQty);
        if (packet.Quantity <= 0)
            return false;
        return machine.GetOrCreateOutput(outPort).TryEnqueue(packet);
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

    public static bool CanOutputAccept(MachineRuntimeState machine, string outPort) =>
        !machine.GetOrCreateOutput(outPort).IsFull;

    public static decimal GetStoredQuantity(TankInternalState tank) => tank.StoredQuantity;

    public static bool TryTankStore(TankInternalState tank, MaterialPacket packet)
    {
        if (packet.Quantity <= 0)
            return true;
        var space = tank.Capacity - tank.StoredQuantity;
        if (space <= 0)
            return false;

        var storeQty = Math.Min(packet.Quantity, space);
        var existing = tank.Storage.FirstOrDefault(p =>
            p.ElementId == packet.ElementId && p.Dna == packet.Dna && p.Quality == packet.Quality);
        if (existing != null)
            existing.Quantity += storeQty;
        else
            tank.Storage.Add(new MaterialPacket
            {
                ElementId = packet.ElementId,
                Dna = packet.Dna,
                Quantity = storeQty,
                Quality = packet.Quality
            });

        packet.Quantity -= storeQty;
        return packet.Quantity <= 0;
    }

    public static MaterialPacket? TryTankWithdraw(TankInternalState tank, decimal maxQty)
    {
        if (tank.Storage.Count == 0 || maxQty <= 0)
            return null;

        var head = tank.Storage[0];
        var qty = Math.Min(head.Quantity, maxQty);
        var pkt = head.Clone();
        pkt.Quantity = qty;
        head.Quantity -= qty;
        if (head.Quantity <= 0)
            tank.Storage.RemoveAt(0);
        return pkt;
    }
}
