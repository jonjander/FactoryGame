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
