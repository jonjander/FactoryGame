namespace FactoryGame.Domain.Simulation;

/// <summary>Deterministic, allocation-free tick summary from machine graph (MVP stub; expand with real machine rules).</summary>
public static class BoardTickSimulator
{
    public static string BuildNote(long globalTick, IReadOnlyList<(string Id, string Type)> machines)
    {
        if (machines.Count == 0)
            return $"tick={globalTick};machines=0";

        long sig = globalTick;
        var ordered = machines.OrderBy(m => m.Id, StringComparer.Ordinal);
        foreach (var (id, type) in ordered)
        {
            sig = Fnv1a64(sig, id);
            sig = Fnv1a64(sig, type);
        }

        return $"tick={globalTick};machines={machines.Count};sig={sig}";
    }

    private static long Fnv1a64(long hash, string s)
    {
        const long offset = -0x340dcf3c1aec2eb7L;
        const long prime = 0x100000001b3L;
        var h = hash == 0 ? offset : hash;
        foreach (var c in s)
        {
            h ^= c;
            h *= prime;
        }
        return h;
    }
}
