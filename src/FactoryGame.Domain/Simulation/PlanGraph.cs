namespace FactoryGame.Domain.Simulation;

public static class PlanGraph
{
    public static IReadOnlyList<string> TopologicalMachineOrder(SimulationPlan plan, out string? cycleError)
    {
        cycleError = null;
        var machineIds = plan.Machines.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        var inDegree = machineIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var adj = machineIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);

        foreach (var c in plan.Connections)
        {
            if (!machineIds.Contains(c.FromId) || !machineIds.Contains(c.ToId))
                continue;
            adj[c.FromId].Add(c.ToId);
            inDegree[c.ToId]++;
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal));
        var order = new List<string>();
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var next in adj[id].OrderBy(x => x, StringComparer.Ordinal))
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        if (order.Count != machineIds.Count)
        {
            cycleError = "Plan contains a cycle in pipe connections.";
            return Array.Empty<string>();
        }

        return order;
    }

    /// <summary>Topological order when acyclic; otherwise stable id order for looped factory lines.</summary>
    public static IReadOnlyList<string> MachineProcessingOrder(SimulationPlan plan, out string? cycleWarning)
    {
        var topo = TopologicalMachineOrder(plan, out var cycleError);
        if (cycleError == null)
        {
            cycleWarning = null;
            return topo;
        }

        cycleWarning = cycleError;
        return plan.Machines.Select(m => m.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    public static bool HasCycle(SimulationPlan plan)
    {
        if (plan.Machines.Count == 0)
            return false;
        var order = TopologicalMachineOrder(plan, out var cycleError);
        return cycleError != null && order.Count == 0;
    }

    public static Dictionary<(string FromId, string FromPort), SimulationConnection> OutgoingByPort(
        SimulationPlan plan) =>
        plan.Connections.ToDictionary(c => (c.FromId, c.FromPort), c => c, comparer: PortKeyComparer.Instance);

    private sealed class PortKeyComparer : IEqualityComparer<(string FromId, string FromPort)>
    {
        public static readonly PortKeyComparer Instance = new();
        public bool Equals((string FromId, string FromPort) x, (string FromId, string FromPort) y) =>
            string.Equals(x.FromId, y.FromId, StringComparison.Ordinal)
            && string.Equals(x.FromPort, y.FromPort, StringComparison.Ordinal);
        public int GetHashCode((string FromId, string FromPort) obj) =>
            HashCode.Combine(obj.FromId, obj.FromPort, StringComparer.Ordinal);
    }
}
